namespace EventTicketing.Models.Responses;

public sealed class TicketPurchaseResponse
{
    public long PurchaseId { get; set; }
    public int PricingTierId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime PurchasedAtUtc { get; set; }
}
