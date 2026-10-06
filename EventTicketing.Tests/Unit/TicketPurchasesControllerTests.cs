using EventTicketing.Api.Controllers;
using EventTicketing.Api.Data;
using EventTicketing.Api.Data.StoredProcedureResults;
using EventTicketing.Models.Requests;
using EventTicketing.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace EventTicketing.Tests.Unit;

[TestFixture]
[Category("Unit")]
public sealed class TicketPurchasesControllerTests
{
    [Test]
    public async Task PurchaseTicket_ReturnsCreatedPurchase()
    {
        var purchase = new TicketPurchaseResponse { PurchaseId = 123, PricingTierId = 7, Quantity = 2 };
        var dataAccess = new FakeEventDataAccess
        {
            PurchaseResult = new OperationResult<TicketPurchaseResult, TicketPurchaseResponse>
            {
                Status = TicketPurchaseResult.Success,
                Data = purchase
            }
        };

        var response = await new TicketPurchasesController(dataAccess)
            .PurchaseTicket(new PurchaseTicketRequest { PricingTierId = 7, Quantity = 2 }, CancellationToken.None);

        var created = response.Result as ObjectResult;
        Assert.Multiple(() =>
        {
            Assert.That(created?.StatusCode, Is.EqualTo(201));
            Assert.That(created?.Value, Is.SameAs(purchase));
        });
    }

    [TestCase(TicketPurchaseResult.PricingTierNotAvailable, typeof(NotFoundResult))]
    [TestCase(TicketPurchaseResult.InsufficientCapacity, typeof(ConflictObjectResult))]
    [TestCase(TicketPurchaseResult.InvalidQuantity, typeof(BadRequestObjectResult))]
    public async Task PurchaseTicket_MapsBusinessFailure(TicketPurchaseResult status, Type expectedResultType)
    {
        var dataAccess = new FakeEventDataAccess
        {
            PurchaseResult = new OperationResult<TicketPurchaseResult, TicketPurchaseResponse>
            {
                Status = status
            }
        };

        var response = await new TicketPurchasesController(dataAccess)
            .PurchaseTicket(new PurchaseTicketRequest { PricingTierId = 7, Quantity = 2 }, CancellationToken.None);

        Assert.That(response.Result, Is.TypeOf(expectedResultType));
    }

    [Test]
    public void PurchaseTicket_DoesNotSwallowTechnicalFailure()
    {
        var failure = new InvalidOperationException("Database unavailable");
        var dataAccess = new FakeEventDataAccess { PurchaseException = failure };
        var controller = new TicketPurchasesController(dataAccess);

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await controller.PurchaseTicket(
                new PurchaseTicketRequest { PricingTierId = 7, Quantity = 1 }, CancellationToken.None));

        Assert.That(thrown, Is.SameAs(failure));
    }
}
