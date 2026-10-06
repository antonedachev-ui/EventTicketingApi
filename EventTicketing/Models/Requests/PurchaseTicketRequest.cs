using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Models.Requests;

public sealed class PurchaseTicketRequest
{
    [Range(1, int.MaxValue)]
    public int PricingTierId { get; set; }
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
