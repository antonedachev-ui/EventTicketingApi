namespace EventTicketing.Models.Responses;

public sealed class EventListItemResponse
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Venue { get; set; } = string.Empty;
    public DateTime EventDateTimeUtc { get; set; }
}
