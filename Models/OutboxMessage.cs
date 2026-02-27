namespace Hive_Movie.Models;

public class OutboxMessage
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = default!;
    public string Payload { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ProcessingAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }

    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}