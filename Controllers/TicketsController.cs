using Hive_Movie.DTOs;
using Hive_Movie.Services.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Hive_Movie.Controllers;

/// <summary>
/// Handles ticket reservations, seat locking, and checkout operations for showtimes.
/// </summary>
/// <remarks>
/// This controller provides endpoints to manage the lifecycle of ticket reservations for movie showtimes:
/// 
/// <list type="bullet">
///   <item>
///     <description><b>Reserve Tickets:</b> Locks selected seats and generates a pending ticket.
///       <list type="bullet">
///         <item><description>Atomic and concurrent-safe: either all requested seats are reserved or the operation fails entirely.</description></item>
///         <item><description>Uses optimistic concurrency to prevent double-booking at the database level.</description></item>
///       </list>
///     </description>
///   </item>
///   <item>
///     <description>Future extensions may include full payment processing, cancellations, and ticket status updates.</description>
///   </item>
/// </list>
/// 
/// All endpoints require an authenticated user (`[Authorize]`). The user's ID is extracted from the JWT token 
/// and used to associate tickets with the account.
/// </remarks>
[Route("api/[controller]")]
[ApiController]
[Tags("Ticketing & Checkout")]
[Authorize] // Only authenticated users can reserve tickets
public class TicketsController(ITicketService ticketService) : ControllerBase
{
    /// <summary>
    /// Starts the checkout process by locking selected seats and generating a pending ticket.
    /// </summary>
    /// <remarks>
    /// - Atomic and concurrent-safe: either all requested seats are reserved or none are.  
    /// - Optimistic concurrency ensures no double-booking occurs, even under high-load scenarios.  
    /// - Returns a `TicketCheckoutResponse` containing the ticket ID, booking reference, total amount, 
    ///   status, and creation timestamp.
    /// </remarks>
    /// <param name="request">The request payload containing the showtime ID and a list of seat coordinates to reserve.</param>
    /// <returns>The newly created pending ticket with booking reference and total amount.</returns>
    /// <response code="201">The seats were successfully locked, and a pending ticket was created.</response>
    /// <response code="400">The request payload is invalid (e.g., missing seats or invalid showtime ID).</response>
    /// <response code="409">One or more seats were already reserved or sold, resulting in a concurrency conflict.</response>
    [HttpPost("reserve")]
    [ProducesResponseType(typeof(TicketCheckoutResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReserveTickets([FromBody] ReserveTicketRequest request)
    {
        var currentUserId = User.FindFirst("id")?.Value ?? throw new UnauthorizedAccessException();

        var response = await ticketService.ReserveTicketsAsync(request, currentUserId);

        return Created(string.Empty, response);
    }

    /// <summary>
    /// Retrieves all tickets (Pending, Confirmed, Cancelled, Expired) for the currently logged-in user.
    /// </summary>
    /// <remarks>
    /// - Requires an authenticated user (`[Authorize]`).  
    /// - Returns a list of `MyTicketResponse` objects, each containing detailed information about a ticket:
    ///   <list type="bullet">
    ///     <item><description>Ticket ID and Booking Reference</description></item>
    ///     <item><description>Movie, Cinema, and Auditorium details</description></item>
    ///     <item><description>Reserved seat coordinates</description></item>
    ///     <item><description>Total amount and ticket status</description></item>
    ///     <item><description>UTC creation timestamp</description></item>
    ///   </list>
    /// - Only tickets associated with the authenticated user's account are returned.
    /// </remarks>
    /// <returns>A list of tickets belonging to the current user.</returns>
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
}