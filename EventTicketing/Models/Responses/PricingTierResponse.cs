namespace EventTicketing.Models.Responses;

public sealed class PricingTierResponse
{
    public int PricingTierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int TotalCapacity { get; set; }
    public int AvailableCapacity { get; set; }
}
