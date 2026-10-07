using EventTicketing.Api.Data.StoredProcedureResults;
using EventTicketing.Models.Requests;
using EventTicketing.Models.Responses;

namespace EventTicketing.Api.Data
{
    public interface IEventDataAccess
    {

        Task<OperationResult<EventCreateResult, EventResponse>> CreateEventAsync(CreateEventRequest request, CancellationToken cancellationToken);
        Task<OperationResult<EventGetResult, EventResponse>> GetEventAsync(int eventId, CancellationToken cancellationToken);
        Task<OperationResult<EventGetAllResult, List<EventListItemResponse>>> GetAllEventsAsync(CancellationToken cancellationToken);
        Task<OperationResult<EventUpdateResult, EventResponse>> UpdateEventAsync(int eventId, UpdateEventRequest request, CancellationToken cancellationToken);
        Task<EventDeleteResult> DeleteEventAsync(int eventId, CancellationToken cancellationToken);
        Task<OperationResult<EventAvailabilityResult, EventAvailabilityResponse>> GetEventAvailabilityAsync(int eventId, CancellationToken cancellationToken);
        Task<OperationResult<EventSalesSummaryResult, EventSalesSummaryResponse>> GetEventSalesSummaryAsync(int eventId, CancellationToken cancellationToken);
        Task<OperationResult<TicketPurchaseResult, TicketPurchaseResponse>> PurchaseTicketAsync(PurchaseTicketRequest request, CancellationToken cancellationToken);
    }
}
