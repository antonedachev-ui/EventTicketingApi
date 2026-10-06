using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Models.Requests;

public sealed class CreateEventRequest
{
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required]
    [StringLength(256)]
    public string Venue { get; set; } = string.Empty;
    public DateTime EventDateTimeUtc { get; set; }
    [Required]
    [MinLength(1)]
    public List<CreatePricingTierRequest> PricingTiers { get; set; } = [];
}
