using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Models.Requests;

public sealed class CreatePricingTierRequest
{
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }
    [Range(1, int.MaxValue)]
    public int TotalCapacity { get; set; }
}
