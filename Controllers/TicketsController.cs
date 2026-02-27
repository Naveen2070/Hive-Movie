using Hive_Movie.DTOs;
using Hive_Movie.Services.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Hive_Movie.Controllers;

/// <summary>
/// Manages ticket reservations, seat locking, and payment confirmation for movie showtimes.
/// </summary>
/// <remarks>
/// This controller handles the lifecycle of ticket bookings:
/// 
/// - **Seat Reservation:** Atomically locks selected seats and creates a pending ticket.
///   - The operation is transactional — either all requested seats are reserved or none are.
///   - Optimistic concurrency control prevents double-booking at the database level.
/// - **Ticket Retrieval:** Allows authenticated users to retrieve all their bookings.
/// - **Payment Confirmation:** Processes asynchronous webhook notifications from the payment provider.
/// 
/// All endpoints require authentication unless explicitly marked with `[AllowAnonymous]`.
/// The authenticated user's ID is extracted from the JWT token and used to associate bookings with the account.
/// </remarks>
[Route("api/[controller]")]
[ApiController]
[Tags("Ticketing & Checkout")]
[Authorize] // Only authenticated users can reserve tickets
public class TicketsController(ITicketService ticketService) : ControllerBase
{
    /// <summary>
    /// Initiates the checkout process by locking selected seats and creating a pending ticket.
    /// </summary>
    /// <remarks>
    /// - Transactional and concurrency-safe: either all seats are reserved or the operation fails.  
    /// - Prevents double-booking through optimistic concurrency checks.  
    /// - Returns a `TicketCheckoutResponse` containing ticket metadata and pricing details.  
    /// - The ticket remains in a `Pending` state until payment is confirmed.
    /// </remarks>
    /// <param name="request">Contains the showtime identifier and the list of seat coordinates to reserve.</param>
    /// <returns>A `TicketCheckoutResponse` representing the created pending booking.</returns>
    /// <response code="201">Seats were successfully locked and a pending ticket was created.</response>
    /// <response code="400">The request is invalid (e.g., missing seats or invalid showtime identifier).</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="409">One or more requested seats are already reserved or sold.</response>
    [HttpPost("reserve")]
    [ProducesResponseType(typeof(TicketCheckoutResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReserveTickets([FromBody] ReserveTicketRequest request)
    {
        var currentUserId = User.FindFirst("id")?.Value ?? throw new UnauthorizedAccessException();

        var response = await ticketService.ReserveTicketsAsync(request, currentUserId);

        return Created(string.Empty, response);
    }

    /// <summary>
    /// Retrieves all tickets associated with the currently authenticated user.
    /// </summary>
    /// <remarks>
    /// Requires authentication. Returns tickets in all states (Pending, Confirmed, Expired).  
    /// 
    /// Each ticket includes:
    /// - Ticket ID and booking reference
    /// - Movie, cinema, and auditorium details
    /// - Reserved seat coordinates
    /// - Total amount and ticket status
    /// - UTC creation timestamp
    /// 
    /// Only tickets belonging to the authenticated user are returned.
    /// </remarks>
    /// <returns>A collection of `MyTicketResponse` objects.</returns>
    /// <response code="200">Successfully retrieved the user's tickets.</response>
    /// <response code="401">The user is not authenticated.</response>
    [HttpGet("my-bookings")]
    [ProducesResponseType(typeof(IEnumerable<MyTicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyTickets()
    {
        var currentUserId = User.FindFirst("id")?.Value ?? throw new UnauthorizedAccessException();

        var response = await ticketService.GetMyTicketsAsync(currentUserId);

        return Ok(response);
    }

    /// <summary>
    /// Handles payment success notifications sent by the external payment provider.
    /// </summary>
    /// <remarks>
    /// This endpoint is intended to be called by the payment gateway (e.g., Stripe or Razorpay)
    /// after a successful transaction.
    /// 
    /// **IMPORTANT:**
    /// In a production environment, the request signature must be cryptographically validated
    /// using the provider’s SDK to ensure authenticity and prevent spoofed requests.
    /// 
    /// A successful confirmation updates the ticket status from `Pending` to `Confirmed`.
    /// Returns HTTP 200 to prevent the payment provider from retrying the webhook.
    /// </remarks>
    /// <param name="payload">Contains the booking reference, transaction ID, and payment status.</param>
    /// <response code="200">Payment confirmed successfully.</response>
    /// <response code="400">The booking cannot be confirmed (e.g., already paid or invalid state).</response>
    /// <response code="404">The specified booking reference does not exist.</response>
    [AllowAnonymous]
    [HttpPost("payment/success")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PaymentSuccessWebhook([FromBody] PaymentWebhookPayload payload)
    {
        await ticketService.ConfirmTicketPaymentAsync(payload.BookingReference);
        return Ok();
    }
}