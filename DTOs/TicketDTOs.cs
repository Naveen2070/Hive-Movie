using System.ComponentModel.DataAnnotations;
namespace Hive_Movie.DTOs;

/// <summary>
///     The payload required to reserve one or more seats for a specific showtime.
/// </summary>
/// <param name="ShowtimeId">The unique identifier (UUID v7) of the showtime for which the seats are being reserved.</param>
/// <param name="Seats">
///     A list of seat coordinates to reserve.
///     Must contain at least one seat. Each seat is represented by its zero-based row and column.
/// </param>
public record ReserveTicketRequest(
    [Required] Guid ShowtimeId,
    [Required][MinLength(1, ErrorMessage = "You must select at least one seat.")]
    List<SeatCoordinateDto> Seats
);

/// <summary>
///     Represents the response after successfully completing a ticket reservation and checkout.
/// </summary>
/// <param name="TicketId">The unique identifier (UUID v7) of the reserved ticket.</param>
/// <param name="BookingReference">A human-readable or system-generated reference code for the booking.</param>
/// <param name="TotalAmount">The total monetary amount charged for the ticket(s).</param>
/// <param name="Status">The current status of the ticket (e.g., Pending, Paid, Cancelled).</param>
/// <param name="CreatedAtUtc">The UTC timestamp when the ticket reservation was created.</param>
public record TicketCheckoutResponse(
    Guid TicketId,
    string BookingReference,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAtUtc
);

/// <summary>
///     Represents detailed information about a reserved ticket, including showtime and cinema info.
/// </summary>
/// <param name="TicketId">The unique identifier (UUID v7) of the reserved ticket.</param>
/// <param name="BookingReference">A human-readable or system-generated reference code for the booking.</param>
/// <param name="MovieTitle">The title of the movie for which the ticket was reserved.</param>
/// <param name="CinemaName">The name of the cinema where the showtime takes place.</param>
/// <param name="AuditoriumName">The name of the auditorium where the showtime occurs.</param>
/// <param name="StartTimeUtc">The UTC start time of the showtime.</param>
/// <param name="ReservedSeats">The list of reserved seats with their row and column coordinates.</param>
/// <param name="TotalAmount">The total monetary amount charged for the ticket(s).</param>
/// <param name="Status">The current status of the ticket (e.g., Pending, Paid, Canceled).</param>
/// <param name="CreatedAtUtc">The UTC timestamp when the ticket reservation was created.</param>
public record MyTicketResponse(
    Guid TicketId,
    string BookingReference,
    string MovieTitle,
    string CinemaName,
    string AuditoriumName,
    DateTime StartTimeUtc,
    List<SeatCoordinateDto> ReservedSeats,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAtUtc
);