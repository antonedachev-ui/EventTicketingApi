namespace EventTicketing.Api.Data.StoredProcedureResults;

public enum TicketPurchaseResult
{
    Success = 0,
    PricingTierNotAvailable = 1,
    InsufficientCapacity = 2,
    InvalidQuantity = 3
}
