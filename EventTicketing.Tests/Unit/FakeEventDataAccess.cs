using EventTicketing.Api.Data;
using EventTicketing.Api.Data.StoredProcedureResults;
using EventTicketing.Models.Requests;
using EventTicketing.Models.Responses;

namespace EventTicketing.Tests.Unit;

internal sealed class FakeEventDataAccess : IEventDataAccess
{
    public OperationResult<EventCreateResult, EventResponse>? CreateResult { get; set; }
    public OperationResult<EventGetResult, EventResponse>? GetResult { get; set; }
    public OperationResult<EventUpdateResult, EventResponse>? UpdateResult { get; set; }
    public EventDeleteResult? DeleteResult { get; set; }
    public OperationResult<EventAvailabilityResult, EventAvailabilityResponse>? AvailabilityResult { get; set; }
    public OperationResult<EventSalesSummaryResult, EventSalesSummaryResponse>? SalesResult { get; set; }
    public OperationResult<TicketPurchaseResult, TicketPurchaseResponse>? PurchaseResult { get; set; }
    public Exception? PurchaseException { get; set; }

    public Task<OperationResult<EventCreateResult, EventResponse>> CreateEventAsync(
        CreateEventRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(CreateResult ?? throw new InvalidOperationException("Create result was not configured."));

    public Task<OperationResult<EventGetResult, EventResponse>> GetEventAsync(
        int eventId, CancellationToken cancellationToken) =>
        Task.FromResult(GetResult ?? throw new InvalidOperationException("Get result was not configured."));

    public Task<OperationResult<EventUpdateResult, EventResponse>> UpdateEventAsync(
        int eventId, UpdateEventRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(UpdateResult ?? throw new InvalidOperationException("Update result was not configured."));

    public Task<EventDeleteResult> DeleteEventAsync(int eventId, CancellationToken cancellationToken) =>
        Task.FromResult(DeleteResult ?? throw new InvalidOperationException("Delete result was not configured."));

    public Task<OperationResult<EventAvailabilityResult, EventAvailabilityResponse>> GetEventAvailabilityAsync(
        int eventId, CancellationToken cancellationToken) =>
        Task.FromResult(AvailabilityResult ?? throw new InvalidOperationException("Availability result was not configured."));

    public Task<OperationResult<EventSalesSummaryResult, EventSalesSummaryResponse>> GetEventSalesSummaryAsync(
        int eventId, CancellationToken cancellationToken) =>
        Task.FromResult(SalesResult ?? throw new InvalidOperationException("Sales result was not configured."));

    public Task<OperationResult<TicketPurchaseResult, TicketPurchaseResponse>> PurchaseTicketAsync(
        PurchaseTicketRequest request, CancellationToken cancellationToken)
    {
        if (PurchaseException is not null)
        {
            throw PurchaseException;
        }

        return Task.FromResult(PurchaseResult ??
            throw new InvalidOperationException("Purchase result was not configured."));
    }
}
