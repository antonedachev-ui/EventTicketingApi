namespace EventTicketing.Models.Responses;

public sealed class EventSalesSummaryResponse
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int TicketsSold { get; set; }
    public decimal TotalRevenue { get; set; }
}
