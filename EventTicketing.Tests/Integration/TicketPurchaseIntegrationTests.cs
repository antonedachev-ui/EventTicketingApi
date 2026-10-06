using EventTicketing.Api.Data;
using EventTicketing.Api.Data.StoredProcedureResults;
using EventTicketing.Models.Requests;
using Microsoft.Extensions.Configuration;

namespace EventTicketing.Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class TicketPurchaseIntegrationTests
{
    private EventDataAccess? _dataAccess;
    private List<int> _createdEventIds = [];

    [SetUp]
    public void SetUp()
    {
        var connectionString = Environment.GetEnvironmentVariable("EVENT_TICKETING_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Ignore("Set EVENT_TICKETING_TEST_CONNECTION_STRING to run SQL Server integration tests.");
            return;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EventTicketing"] = connectionString
            })
            .Build();

        _dataAccess = new EventDataAccess(new SqlStoredProcedureExecutor(configuration));
        _createdEventIds = [];
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_dataAccess is null)
        {
            return;
        }

        foreach (var eventId in _createdEventIds)
        {
            // Retain historical rows as the application does; unique names make reruns independent.
            var result = await _dataAccess.DeleteEventAsync(eventId, CancellationToken.None);
            Assert.That(result, Is.EqualTo(EventDeleteResult.Success));
        }
    }

    [Test]
    public async Task CreateEvent_CanBeRetrievedWithItsPricingTier()
    {
        var created = await CreateEventAsync(capacity: 3, price: 12.50m);

        var fetched = await _dataAccess!.GetEventAsync(created.EventId, CancellationToken.None);

        Assert.That(fetched.Status, Is.EqualTo(EventGetResult.Success));
        Assert.Multiple(() =>
        {
            Assert.That(fetched.Data?.EventId, Is.EqualTo(created.EventId));
            Assert.That(fetched.Data?.Name, Is.EqualTo(created.Name));
            Assert.That(fetched.Data?.PricingTiers, Has.Count.EqualTo(1));
            Assert.That(fetched.Data?.PricingTiers[0].AvailableCapacity, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task SuccessfulPurchase_DecrementsInventoryAndRecordsPurchasePrice()
    {
        var created = await CreateEventAsync(capacity: 3, price: 12.50m);
        var tierId = created.PricingTiers.Single().PricingTierId;

        var purchase = await _dataAccess!.PurchaseTicketAsync(
            new PurchaseTicketRequest { PricingTierId = tierId, Quantity = 2 }, CancellationToken.None);
        var availability = await _dataAccess.GetEventAvailabilityAsync(created.EventId, CancellationToken.None);
        var sales = await _dataAccess.GetEventSalesSummaryAsync(created.EventId, CancellationToken.None);

        Assert.That(purchase.Status, Is.EqualTo(TicketPurchaseResult.Success));
        Assert.Multiple(() =>
        {
            Assert.That(purchase.Data?.PurchaseId, Is.GreaterThan(0));
            Assert.That(purchase.Data?.UnitPrice, Is.EqualTo(12.50m));
            Assert.That(purchase.Data?.Quantity, Is.EqualTo(2));
            Assert.That(purchase.Data?.PurchasedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(availability.Data?.PricingTiers.Single().AvailableCapacity, Is.EqualTo(1));
            Assert.That(sales.Data?.TicketsSold, Is.EqualTo(2));
            Assert.That(sales.Data?.TotalRevenue, Is.EqualTo(25m));
        });
    }

    [Test]
    public async Task InsufficientCapacity_DoesNotChangeInventoryOrSales()
    {
        var created = await CreateEventAsync(capacity: 1, price: 12.50m);
        var tierId = created.PricingTiers.Single().PricingTierId;

        var purchase = await _dataAccess!.PurchaseTicketAsync(
            new PurchaseTicketRequest { PricingTierId = tierId, Quantity = 2 }, CancellationToken.None);
        var availability = await _dataAccess.GetEventAvailabilityAsync(created.EventId, CancellationToken.None);
        var sales = await _dataAccess.GetEventSalesSummaryAsync(created.EventId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(purchase.Status, Is.EqualTo(TicketPurchaseResult.InsufficientCapacity));
            Assert.That(purchase.Data, Is.Null);
            Assert.That(availability.Data?.PricingTiers.Single().AvailableCapacity, Is.EqualTo(1));
            Assert.That(sales.Data?.TicketsSold, Is.Zero);
        });
    }

    [Test]
    public async Task ConcurrentPurchases_CannotOversell()
    {
        const int capacity = 5;
        const int attempts = 16;
        var created = await CreateEventAsync(capacity, price: 12.50m);
        var tierId = created.PricingTiers.Single().PricingTierId;

        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var purchases = Enumerable.Range(0, attempts).Select(async _ =>
        {
            await start.Task;
            return await _dataAccess!.PurchaseTicketAsync(
                new PurchaseTicketRequest { PricingTierId = tierId, Quantity = 1 }, CancellationToken.None);
        }).ToArray();

        start.SetResult(true);
        var results = await Task.WhenAll(purchases);
        var availability = await _dataAccess!.GetEventAvailabilityAsync(created.EventId, CancellationToken.None);
        var sales = await _dataAccess.GetEventSalesSummaryAsync(created.EventId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(results.Count(x => x.Status == TicketPurchaseResult.Success), Is.EqualTo(capacity));
            Assert.That(results.Count(x => x.Status == TicketPurchaseResult.InsufficientCapacity),
                Is.EqualTo(attempts - capacity));
            Assert.That(availability.Data?.PricingTiers.Single().AvailableCapacity, Is.Zero);
            Assert.That(sales.Data?.TicketsSold, Is.EqualTo(capacity));
        });
    }

    private async Task<EventTicketing.Models.Responses.EventResponse> CreateEventAsync(int capacity, decimal price)
    {
        var request = new CreateEventRequest
        {
            Name = $"Integration-{Guid.NewGuid():N}",
            Venue = "Integration test venue",
            EventDateTimeUtc = DateTime.UtcNow.AddDays(7),
            PricingTiers =
            [
                new CreatePricingTierRequest
                {
                    Name = "Standard",
                    Price = price,
                    TotalCapacity = capacity
                }
            ]
        };

        var result = await _dataAccess!.CreateEventAsync(request, CancellationToken.None);
        if (result.Data is { EventId: > 0 } created)
        {
            _createdEventIds.Add(created.EventId);
        }

        Assert.That(result.Status, Is.EqualTo(EventCreateResult.Success));
        Assert.That(result.Data, Is.Not.Null);
        return result.Data!;
    }
}
