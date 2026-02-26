using Hive_Movie.DTOs;
using Hive_Movie.Services.ShowTimes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Hive_Movie.Controllers;

/// <summary>
/// Provides endpoints for managing showtimes and handling ticket reservations.
/// </summary>
/// <remarks>
/// This controller exposes both public ticketing endpoints and restricted organizer
/// management endpoints secured via role-based access control (RBAC).
/// </remarks>
[Route("api/[controller]")]
[ApiController]
[Tags("Showtime Managment")]
public class ShowtimesController(IShowtimeService showtimeService) : ControllerBase
{
    /// <summary>
    /// Retrieves the real-time seat map for a specific showtime.
    /// </summary>
    /// <remarks>
    /// Accessible to anonymous users.  
    /// Translates the underlying high-performance seat state storage into a
    /// JSON structure suitable for frontend rendering (e.g., CSS grid).
    /// </remarks>
    /// <param name="id">The unique identifier (UUID v7) of the showtime.</param>
    /// <response code="200">Successfully retrieved the seat map.</response>
    /// <response code="404">The specified showtime does not exist.</response>
    [AllowAnonymous]
    [HttpGet("{id:guid}/seatmap")]
    [Tags("Ticketing & Checkout")]
    [ProducesResponseType(typeof(ShowtimeSeatMapResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSeatMap(Guid id)
    {
        return Ok(await showtimeService.GetSeatMapAsync(id));
    }

    /// <summary>
    /// Attempts to reserve a group of seats for a specific showtime.
    /// </summary>
    /// <remarks>
    /// Requires authentication.  
    /// This operation is atomic and concurrency-safe.  
    /// Either all requested seats are successfully reserved, or the operation fails entirely.
    /// </remarks>
    /// <param name="id">The unique identifier (UUID v7) of the showtime.</param>
    /// <param name="request">The list of seat coordinates to reserve.</param>
    /// <response code="200">All requested seats were successfully reserved.</response>
    /// <response code="400">One or more seats are invalid, out of bounds, or already taken.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="404">The specified showtime does not exist.</response>
    /// <response code="409">A concurrency conflict occurred while reserving seats.</response>
    [Authorize]
    [HttpPost("{id:guid}/reserve")]
    [Tags("Ticketing & Checkout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReserveSeats(Guid id, [FromBody] ReserveSeatsRequest request)
    {
        await showtimeService.ReserveSeatsAsync(id, request);
        return Ok(new
        {
            Message = "Seats successfully reserved!"
        });
    }

    // -------------------------------
    // ORGANIZER SHOWTIME MANAGEMENT
    // -------------------------------

    /// <summary>
    /// Creates a new showtime for a movie in a specific auditorium.
    /// </summary>
    /// <remarks>
    /// Restricted to users with roles:
    /// 
    /// <list type="bullet">
    /// <item><description>ROLE_ORGANIZER</description></item>
    /// <item><description>ROLE_SUPER_ADMIN</description></item>
    /// </list>
    /// 
    /// Organizers may only create showtimes for cinemas they own.  
    /// Super administrators may create showtimes for any cinema.
    /// </remarks>
    /// <param name="request">The showtime creation payload.</param>
    /// <response code="201">The showtime was successfully created.</response>
    /// <response code="400">The request payload is invalid.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpPost]
    [Tags("Showtimes Management")]
    [ProducesResponseType(typeof(ShowtimeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateShowtimeRequest request)
    {
        var currentUser = User.FindFirst("id")?.Value ?? throw new UnauthorizedAccessException();
        var isAdmin = User.IsInRole("ROLE_SUPER_ADMIN");

        var showtime = await showtimeService.CreateShowtimeAsync(request, currentUser, isAdmin);
        return Created(string.Empty, showtime);
    }

    /// <summary>
    /// Updates an existing showtime.
    /// </summary>
    /// <remarks>
    /// Restricted to users with roles:
    /// <list type="bullet">
    /// <item>ROLE_ORGANIZER</item>
    /// <item>ROLE_SUPER_ADMIN</item>
    /// </list>
    /// Organizers may only update showtimes belonging to their own cinemas.
    /// </remarks>
    /// <param name="id">The unique identifier of the showtime.</param>
    /// <param name="request">The updated showtime details.</param>
    /// <response code="204">The showtime was successfully updated.</response>
    /// <response code="400">The request payload is invalid.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    /// <response code="404">The showtime does not exist.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpPut("{id:guid}")]
    [Tags("Showtimes Management")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateShowtimeRequest request)
    {
        var currentUser = User.FindFirst("id")?.Value ?? throw new UnauthorizedAccessException();
        var isAdmin = User.IsInRole("ROLE_SUPER_ADMIN");

        await showtimeService.UpdateShowtimeAsync(id, request, currentUser, isAdmin);
        return NoContent();
    }

    /// <summary>
    /// Deletes an existing showtime.
    /// </summary>
    /// <remarks>
    /// Restricted to users with roles:
    /// <list type="bullet">
    /// <item>ROLE_ORGANIZER</item>
    /// <item>ROLE_SUPER_ADMIN</item>
    /// </list>
    /// Organizers may only delete showtimes belonging to their own cinemas.
    /// </remarks>
    /// <param name="id">The unique identifier of the showtime.</param>
    /// <response code="204">The showtime was successfully deleted.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    /// <response code="404">The showtime does not exist.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpDelete("{id:guid}")]
    [Tags("Showtimes Management")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var currentUser = User.FindFirst("id")?.Value ?? throw new UnauthorizedAccessException();
        var isAdmin = User.IsInRole("ROLE_SUPER_ADMIN");

        await showtimeService.DeleteShowtimeAsync(id, currentUser, isAdmin);
        return NoContent();
    }
}