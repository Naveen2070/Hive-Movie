using System.ComponentModel.DataAnnotations;
namespace Hive_Movie.DTOs;

/// <summary>
///     The payload required to reserve one or more seats for a specific showtime.
/// </summary>
/// <param name="ShowtimeId">The unique identifier (UUID v7) of the showtime for which the seats are being reserved. <example>8bc45f12-9831-4562-c1fc-1a983f44efc1</example></param>
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
/// <param name="TicketId">The unique identifier (UUID v7) of the reserved ticket. <example>d290f1ee-6c54-4b01-90e6-d701748f0851</example></param>
/// <param name="BookingReference">A human-readable or system-generated reference code for the booking. <example>HIVE-A1B2C3</example></param>
/// <param name="TotalAmount">The total monetary amount charged for the ticket(s). <example>31.00</example></param>
/// <param name="Status">The current status of the ticket (e.g., Pending, Paid, Cancelled). <example>Pending</example></param>
/// <param name="CreatedAtUtc">The UTC timestamp when the ticket reservation was created. <example>2026-05-10T14:23:05Z</example></param>
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
/// <param name="TicketId">The unique identifier (UUID v7) of the reserved ticket. <example>d290f1ee-6c54-4b01-90e6-d701748f0851</example></param>
/// <param name="BookingReference">A human-readable or system-generated reference code for the booking. <example>HIVE-A1B2C3</example></param>
/// <param name="MovieTitle">The title of the movie for which the ticket was reserved. <example>The Matrix</example></param>
/// <param name="CinemaName">The name of the cinema where the showtime takes place. <example>Hive Multiplex Downtown</example></param>
/// <param name="AuditoriumName">The name of the auditorium where the showtime occurs. <example>IMAX Screen 1</example></param>
/// <param name="StartTimeUtc">The UTC start time of the showtime. <example>2026-10-31T19:30:00Z</example></param>
/// <param name="ReservedSeats">The list of reserved seats with their row and column coordinates.</param>
/// <param name="TotalAmount">The total monetary amount charged for the ticket(s). <example>31.00</example></param>
/// <param name="Status">The current status of the ticket (e.g., Pending, Paid, Canceled). <example>Confirmed</example></param>
/// <param name="CreatedAtUtc">The UTC timestamp when the ticket reservation was created. <example>2026-05-10T14:23:05Z</example></param>
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

/// <summary>
///     Represents the payload sent by the payment provider webhook.
/// </summary>
/// <param name="BookingReference">
///     The booking reference associated with the payment. <example>HIVE-A1B2C3</example>
/// </param>
/// <param name="TransactionId">
///     The unique identifier of the payment transaction from the provider. <example>pi_3MtwBwLkdIwHu7ix28a3tqND</example>
/// </param>
/// <param name="Status">
///     The payment status reported by the provider (e.g., succeeded, failed). <example>succeeded</example>
/// </param>
public record PaymentWebhookPayload(
    string BookingReference,
    string TransactionId,
    string Status
);