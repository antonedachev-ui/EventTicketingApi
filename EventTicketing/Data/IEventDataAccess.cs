using EventTicketing.Api.Data.StoredProcedureResults;
using EventTicketing.Models.Responses;

namespace EventTicketing.Api.Data
{
    public interface IEventDataAccess
    {

        Task<OperationResult<EventGetResult, EventResponse>> GetEventAsync(int eventId, CancellationToken cancellationToken);
    }
}
