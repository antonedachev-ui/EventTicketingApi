using EventTicketing.Api.Data;
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

        [HttpGet("{eventId:int}")]
        public async Task<ActionResult<EventResponse>> GetEvent(int eventId,CancellationToken cancellationToken)
        {
            var result = await _eventDataAccess.GetEventAsync(eventId,cancellationToken);

            return result.ToActionResult(this);
        }
    }
}
