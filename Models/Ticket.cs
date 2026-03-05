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

/// <summary>
///     Represents the lifecycle state of a ticket reservation.
/// </summary>
public enum TicketStatus
{
    /// <summary>
    ///     Seats are locked, but payment has not yet been confirmed.
    /// </summary>
    Pending,

    /// <summary>
    ///     Payment is confirmed and the ticket is valid for entry.
    /// </summary>
    Confirmed,

    /// <summary>
    ///     The ticket was successfully scanned and the attendee has entered the venue.
    /// </summary>
    Used,

    /// <summary>
    ///     The showtime has passed and the ticket was never used.
    /// </summary>
    Expired,

    /// <summary>
    ///     The ticket was cancelled by the user or the organizer.
    /// </summary>
    Cancelled
}