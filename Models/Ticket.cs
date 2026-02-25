using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
namespace Hive_Movie.Models;

public class Ticket : BaseAuditableEntity
{
    [MaxLength(255)]
    public required string UserId { get; init; }

    public required Guid ShowtimeId { get; init; }

    [MaxLength(20)]
    public required string BookingReference { get; init; }

    [Precision(18, 2)]
    public List<SeatCoordinate> ReservedSeats { get; set; } = [];

    public decimal TotalAmount { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Pending;

    public DateTime? PaidAtUtc { get; set; }

    public Showtime? Showtime { get; init; }
}

public enum TicketStatus
{
    Pending,
    Confirmed,
    Expired,
    Cancelled
}