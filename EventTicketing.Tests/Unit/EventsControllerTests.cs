using EventTicketing.Api.Controllers;
using EventTicketing.Api.Data;
using EventTicketing.Api.Data.StoredProcedureResults;
using EventTicketing.Models.Requests;
using EventTicketing.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace EventTicketing.Tests.Unit;

[TestFixture]
[Category("Unit")]
public sealed class EventsControllerTests
{
    [Test]
    public async Task CreateEvent_ReturnsCreatedWithGetEventRoute()
    {
        var eventResponse = new EventResponse { EventId = 42, Name = "Concert" };
        var dataAccess = new FakeEventDataAccess
        {
            CreateResult = new OperationResult<EventCreateResult, EventResponse>
            {
                Status = EventCreateResult.Success,
                Data = eventResponse
            }
        };

        var response = await new EventsController(dataAccess)
            .CreateEvent(new CreateEventRequest(), CancellationToken.None);

        var created = response.Result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(created!.StatusCode, Is.EqualTo(201));
            Assert.That(created.ActionName, Is.EqualTo(nameof(EventsController.GetEvent)));
            Assert.That(created.RouteValues!["eventId"], Is.EqualTo(42));
            Assert.That(created.Value, Is.SameAs(eventResponse));
        });
    }

    [TestCase(EventCreateResult.PricingTierMissing)]
    [TestCase(EventCreateResult.DuplicatePricingTierNames)]
    public async Task CreateEvent_MapsInvalidTierInputToBadRequest(EventCreateResult status)
    {
        var dataAccess = new FakeEventDataAccess
        {
            CreateResult = new OperationResult<EventCreateResult, EventResponse> { Status = status }
        };

        var response = await new EventsController(dataAccess)
            .CreateEvent(new CreateEventRequest(), CancellationToken.None);

        var badRequest = response.Result as BadRequestObjectResult;
        Assert.That(badRequest?.Value, Is.TypeOf<ProblemDetails>());
        Assert.That(((ProblemDetails)badRequest!.Value!).Status, Is.EqualTo(400));
    }

    [Test]
    public async Task GetEvent_ReturnsEvent()
    {
        var eventResponse = new EventResponse { EventId = 42, Name = "Concert" };
        var dataAccess = new FakeEventDataAccess
        {
            GetResult = new OperationResult<EventGetResult, EventResponse>
            {
                Status = EventGetResult.Success,
                Data = eventResponse
            }
        };

        var response = await new EventsController(dataAccess).GetEvent(42, CancellationToken.None);

        Assert.That((response.Result as OkObjectResult)?.Value, Is.SameAs(eventResponse));
    }

    [Test]
    public async Task GetEvent_ReturnsNotFound()
    {
        var dataAccess = new FakeEventDataAccess
        {
            GetResult = new OperationResult<EventGetResult, EventResponse>
            {
                Status = EventGetResult.EventNotFound
            }
        };

        var response = await new EventsController(dataAccess).GetEvent(42, CancellationToken.None);

        Assert.That(response.Result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task GetAllEvents_ReturnsEventListWithoutPricingTiers()
    {
        var events = new List<EventListItemResponse>
        {
            new() { EventId = 42, Name = "Concert", Venue = "Hall", EventDateTimeUtc = DateTime.UtcNow }
        };
        var dataAccess = new FakeEventDataAccess
        {
            GetAllResult = new OperationResult<EventGetAllResult, List<EventListItemResponse>>
            {
                Status = EventGetAllResult.Success,
                Data = events
            }
        };

        var response = await new EventsController(dataAccess).GetAllEvents(CancellationToken.None);

        Assert.That((response.Result as OkObjectResult)?.Value, Is.SameAs(events));
    }

    [Test]
    public async Task GetAllEvents_ReturnsNotFoundWhenNoEventsExist()
    {
        var dataAccess = new FakeEventDataAccess
        {
            GetAllResult = new OperationResult<EventGetAllResult, List<EventListItemResponse>>
            {
                Status = EventGetAllResult.EventNotFound,
                Data = []
            }
        };

        var response = await new EventsController(dataAccess).GetAllEvents(CancellationToken.None);

        Assert.That(response.Result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task UpdateEvent_ReturnsUpdatedEvent()
    {
        var eventResponse = new EventResponse { EventId = 42, Name = "Renamed concert" };
        var dataAccess = new FakeEventDataAccess
        {
            UpdateResult = new OperationResult<EventUpdateResult, EventResponse>
            {
                Status = EventUpdateResult.Success,
                Data = eventResponse
            }
        };

        var response = await new EventsController(dataAccess)
            .UpdateEvent(42, new UpdateEventRequest(), CancellationToken.None);

        Assert.That((response.Result as OkObjectResult)?.Value, Is.SameAs(eventResponse));
    }

    [TestCase(EventUpdateResult.EventNotFound, typeof(NotFoundResult))]
    [TestCase(EventUpdateResult.InvalidPricingTierId, typeof(BadRequestObjectResult))]
    [TestCase(EventUpdateResult.PricingTierInvalidTotalCapacity, typeof(ConflictObjectResult))]
    public async Task UpdateEvent_MapsBusinessFailure(EventUpdateResult status, Type expectedResultType)
    {
        var dataAccess = new FakeEventDataAccess
        {
            UpdateResult = new OperationResult<EventUpdateResult, EventResponse> { Status = status }
        };

        var response = await new EventsController(dataAccess)
            .UpdateEvent(42, new UpdateEventRequest(), CancellationToken.None);

        Assert.That(response.Result, Is.TypeOf(expectedResultType));
    }

    [TestCase(EventDeleteResult.Success, typeof(NoContentResult))]
    [TestCase(EventDeleteResult.EventNotFound, typeof(NotFoundResult))]
    public async Task DeleteEvent_MapsResult(EventDeleteResult status, Type expectedResultType)
    {
        var dataAccess = new FakeEventDataAccess { DeleteResult = status };

        var response = await new EventsController(dataAccess).DeleteEvent(42, CancellationToken.None);

        Assert.That(response, Is.TypeOf(expectedResultType));
    }

    [Test]
    public async Task Availability_ReturnsOkWithEmptyTiers()
    {
        var availability = new EventAvailabilityResponse { EventId = 42 };
        var dataAccess = new FakeEventDataAccess
        {
            AvailabilityResult = new OperationResult<EventAvailabilityResult, EventAvailabilityResponse>
            {
                Status = EventAvailabilityResult.Success,
                Data = availability
            }
        };

        var response = await new EventsController(dataAccess)
            .GetEventAvailability(42, CancellationToken.None);

        Assert.That((response.Result as OkObjectResult)?.Value, Is.SameAs(availability));
        Assert.That(availability.PricingTiers, Is.Empty);
    }

    [Test]
    public async Task SalesSummary_ReturnsHistoricalSummary()
    {
        var summary = new EventSalesSummaryResponse
        {
            EventId = 42,
            TicketsSold = 2,
            TotalRevenue = 25m
        };
        var dataAccess = new FakeEventDataAccess
        {
            SalesResult = new OperationResult<EventSalesSummaryResult, EventSalesSummaryResponse>
            {
                Status = EventSalesSummaryResult.Success,
                Data = summary
            }
        };

        var response = await new EventsController(dataAccess)
            .GetEventSalesSummary(42, CancellationToken.None);

        Assert.That((response.Result as OkObjectResult)?.Value, Is.SameAs(summary));
    }

    [Test]
    public async Task SalesSummary_ReturnsNotFoundWhenProcedureReturnsNoRow()
    {
        var dataAccess = new FakeEventDataAccess
        {
            SalesResult = new OperationResult<EventSalesSummaryResult, EventSalesSummaryResponse>
            {
                Status = EventSalesSummaryResult.Success
            }
        };

        var response = await new EventsController(dataAccess)
            .GetEventSalesSummary(42, CancellationToken.None);

        Assert.That(response.Result, Is.TypeOf<NotFoundResult>());
    }
}
