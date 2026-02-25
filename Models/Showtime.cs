using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Hive_Movie.Models;

public class Showtime : BaseAuditableEntity
{
    public Guid MovieId { get; init; }
    public Guid AuditoriumId { get; init; }

    public DateTime StartTimeUtc { get; init; }

    [Precision(18, 2)]
    public decimal BasePrice { get; init; }

    // THE HIGH PERFORMANCE SEAT ENGINE DATA
    public required byte[] SeatAvailabilityState { get; init; }

    // EF Core Concurrency Token (Optimistic Locking)
    [Timestamp]
    public byte[]? RowVersion { get; init; }

    // Navigation Properties
    public Movie? Movie { get; init; }
    public Auditorium? Auditorium { get; init; }
}