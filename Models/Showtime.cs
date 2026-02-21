using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Hive_Movie.Models
{
    public class Showtime : BaseAuditableEntity
    {
        public Guid MovieId { get; set; }
        public Guid AuditoriumId { get; set; }

        public DateTime StartTimeUtc { get; set; }

        [Precision(18, 2)]
        public decimal BasePrice { get; set; }

        // THE HIGH PERFORMANCE SEAT ENGINE DATA
        public required byte[] SeatAvailabilityState { get; set; }

        // EF Core Concurrency Token (Optimistic Locking)
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // Navigation Properties
        public Movie? Movie { get; set; }
        public Auditorium? Auditorium { get; set; }
    }
}
