using Hive_Movie.DTOs;
using Hive_Movie.Services.ShowTimes;
using Microsoft.AspNetCore.Mvc;

namespace Hive_Movie.Controllers;

[Route("api/[controller]/{id:guid}")]
[ApiController]
[Tags("Ticketing & Showtimes")] 
public class ShowtimesController(IShowtimeService showtimeService) : ControllerBase
{
    private readonly IShowtimeService _showtimeService = showtimeService;

    /// <summary>
    /// Retrieves the real-time seat map for a specific showtime.
    /// </summary>
    /// <remarks>
    /// Translates the underlying high-performance byte array into a JSON array 
    /// suitable for rendering a CSS grid on the frontend.
    /// </remarks>
    /// <param name="id">The UUID v7 of the showtime.</param>
    /// <returns>The complete seating layout and current availability status.</returns>
    /// <response code="200">Successfully retrieved the seat map.</response>
    /// <response code="404">The specified showtime does not exist.</response>
    [HttpGet("seatmap")]
    [ProducesResponseType(typeof(ShowtimeSeatMapResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSeatMap(Guid id)
    {
        var response = await _showtimeService.GetSeatMapAsync(id);
        return Ok(response);
    }

    /// <summary>
    /// Attempts to reserve a specific group of seats.
    /// </summary>
    /// <remarks>
    /// This operation is highly concurrent and atomic. It either reserves all requested seats 
    /// or fails entirely. It uses optimistic concurrency control (RowVersion) to prevent double-booking 
    /// at the database level.
    /// </remarks>
    /// <param name="id">The UUID v7 of the showtime.</param>
    /// <param name="request">The list of seat coordinates to reserve.</param>
    /// <response code="200">All requested seats were successfully reserved.</response>
    /// <response code="400">One or more seats are out of bounds or already taken.</response>
    /// <response code="404">The specified showtime does not exist.</response>
    /// <response code="409">Concurrency conflict: Another user booked these seats at the exact same millisecond.</response>
    [HttpPost("reserve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReserveSeats(Guid id, [FromBody] ReserveSeatsRequest request)
    {
        await _showtimeService.ReserveSeatsAsync(id, request);

        return Ok(new { Message = "Seats successfully reserved!" });
    }
}