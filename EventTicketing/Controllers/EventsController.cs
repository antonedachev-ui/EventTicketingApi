using EventTicketing.Api.Data;
using EventTicketing.Models.Requests;
using EventTicketing.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace EventTicketing.Api.Controllers
{
    [ApiController]
    [Route("api/events")]
    public sealed class EventsController : ControllerBase
    {
        private readonly IEventDataAccess _eventDataAccess;

        public EventsController(IEventDataAccess eventDataAccess)
        {
            _eventDataAccess = eventDataAccess;
        }

        [HttpPost]
        public async Task<ActionResult<EventResponse>> CreateEvent(
            [FromBody] CreateEventRequest request, CancellationToken cancellationToken)
        {
            var result = await _eventDataAccess.CreateEventAsync(request, cancellationToken);
            return result.ToActionResult(this);
        }

        [HttpGet("{eventId:int}")]
        public async Task<ActionResult<EventResponse>> GetEvent(int eventId, CancellationToken cancellationToken)
        {
            var result = await _eventDataAccess.GetEventAsync(eventId, cancellationToken);

            return result.ToActionResult(this);
        }

        [HttpGet]
        public async Task<ActionResult<List<EventListItemResponse>>> GetAllEvents(CancellationToken cancellationToken)
        {
            var result = await _eventDataAccess.GetAllEventsAsync(cancellationToken);
            return result.ToActionResult(this);
        }

        [HttpPut("{eventId:int}")]
        public async Task<ActionResult<EventResponse>> UpdateEvent(
            int eventId, [FromBody] UpdateEventRequest request, CancellationToken cancellationToken)
        {
            var result = await _eventDataAccess.UpdateEventAsync(eventId, request, cancellationToken);
            return result.ToActionResult(this);
        }

        [HttpDelete("{eventId:int}")]
        public async Task<IActionResult> DeleteEvent(int eventId, CancellationToken cancellationToken)
        {
            var result = await _eventDataAccess.DeleteEventAsync(eventId, cancellationToken);
            return result.ToActionResult(this);
        }

        [HttpGet("{eventId:int}/availability")]
        public async Task<ActionResult<EventAvailabilityResponse>> GetEventAvailability(
            int eventId, CancellationToken cancellationToken)
        {
            var result = await _eventDataAccess.GetEventAvailabilityAsync(eventId, cancellationToken);
            return result.ToActionResult(this);
        }

        [HttpGet("{eventId:int}/sales-summary")]
        public async Task<ActionResult<EventSalesSummaryResponse>> GetEventSalesSummary(
            int eventId, CancellationToken cancellationToken)
        {
            var result = await _eventDataAccess.GetEventSalesSummaryAsync(eventId, cancellationToken);
            return result.ToActionResult(this);
        }
    }
}
