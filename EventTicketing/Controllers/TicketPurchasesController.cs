using EventTicketing.Api.Data;
using EventTicketing.Models.Requests;
using EventTicketing.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace EventTicketing.Api.Controllers;

[ApiController]
[Route("api/ticket-purchases")]
public sealed class TicketPurchasesController : ControllerBase
{
    private readonly IEventDataAccess _eventDataAccess;

    public TicketPurchasesController(IEventDataAccess eventDataAccess)
    {
        _eventDataAccess = eventDataAccess;
    }

    [HttpPost]
    public async Task<ActionResult<TicketPurchaseResponse>> PurchaseTicket(
        [FromBody] PurchaseTicketRequest request, CancellationToken cancellationToken)
    {
        var result = await _eventDataAccess.PurchaseTicketAsync(request, cancellationToken);
        return result.ToActionResult(this);
    }
}
