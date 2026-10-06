namespace EventTicketing.Models.Responses;

public sealed class EventAvailabilityResponse
{
    public int EventId { get; set; }
    public List<PricingTierResponse> PricingTiers { get; set; } = [];
}
