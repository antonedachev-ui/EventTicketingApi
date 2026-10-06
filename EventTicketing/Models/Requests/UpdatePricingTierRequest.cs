using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Models.Requests;

public sealed class UpdatePricingTierRequest
{
    [Range(1, int.MaxValue)]
    public int? PricingTierId { get; set; }
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "99999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; set; }
    [Range(1, int.MaxValue)]
    public int TotalCapacity { get; set; }
}
