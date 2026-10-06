namespace EventTicketing.Api.Data.StoredProcedureResults;

public enum EventUpdateResult
{
    Success = 0,
    EventNotFound = 1,
    EventNameInvalid = 2,
    VenueNameInvalid = 3,
    PricingTierMissing = 4,
    PricingTierNameInvalid = 5,
    DuplicatePricingTierNames = 6,
    PricingTierInvalidPrice = 7,
    PricingTierInvalidTotalCapacity = 8,
    InvalidPricingTierId = 9
}
