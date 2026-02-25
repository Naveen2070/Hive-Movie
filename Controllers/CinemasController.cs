using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.Cinemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace Hive_Movie.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Cinemas Management")]
public class CinemasController( ICinemaService cinemaService) : ControllerBase
{
    /// <summary>
    /// Retrieves all physical cinema locations.
    /// </summary>
    /// <remarks>
    /// Fetches a complete list of all active cinema branches in the system.
    /// Soft-deleted cinemas are automatically excluded.
    /// </remarks>
    /// <returns>A list of cinemas.</returns>
    /// <response code="200">Successfully retrieved the list of cinemas.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CinemaResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await cinemaService.GetAllCinemasAsync());
    }

    /// <summary>
    /// Retrieves a specific cinema by its unique identifier.
    /// </summary>
    /// <param name="id">The UUID v7 of the cinema to retrieve.</param>
    /// <returns>The requested cinema details.</returns>
    /// <response code="200">The cinema was found and returned successfully.</response>
    /// <response code="404">No cinema exists with the provided ID, or it has been deleted.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CinemaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        return Ok(await cinemaService.GetCinemaByIdAsync(id));
    }

    /// <summary>
    /// Registers a new cinema location.
    /// </summary>
    /// <remarks>
    /// Creates a new physical cinema record. The system will automatically generate a 
    /// sequential UUID v7.
    /// </remarks>
    /// <param name="request">The cinema details to save.</param>
    /// <returns>The newly created cinema.</returns>
    /// <response code="201">The cinema was successfully created.</response>
    /// <response code="400">The request payload failed validation (e.g., missing name or location).</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpPost]
    [ProducesResponseType(typeof(CinemaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCinemaRequest request)
    {
        var organizerId = User.FindFirst("id")?.Value
            ?? throw new UnauthorizedAccessException("Missing User Id.");

        var cinema = await cinemaService.CreateCinemaAsync(request,organizerId);
        return CreatedAtAction(nameof(GetById), new { id = cinema.Id }, cinema);
    }

    [Authorize(Roles = "ROLE_SUPER_ADMIN")]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] CinemaApprovalStatus status)
    {
        await cinemaService.UpdateCinemaStatusAsync(id, status);
        return NoContent();
    }

    /// <summary>
    /// Updates a cinema's details.
    /// </summary>
    /// <remarks>
    /// Performs a full replacement (PUT) of the cinema's data. All required fields 
    /// must be provided in the request body.
    /// </remarks>
    /// <param name="id">The UUID v7 of the cinema to update.</param>
    /// <param name="request">The complete updated cinema details.</param>
    /// <response code="204">The cinema was successfully updated.</response>
    /// <response code="400">The request payload failed validation.</response>
    /// <response code="404">No cinema exists with the provided ID.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCinemaRequest request)
    {
        await cinemaService.UpdateCinemaAsync(id, request);
        return NoContent();
    }

    /// <summary>
    /// Removes a cinema location from the system.
    /// </summary>
    /// <remarks>
    /// Performs a soft-delete on the cinema. The record remains in the database for historical 
    /// auditing but is hidden from standard API queries.
    /// </remarks>
    /// <param name="id">The UUID v7 of the cinema to delete.</param>
    /// <response code="204">The cinema was successfully deleted.</response>
    /// <response code="404">No cinema exists with the provided ID.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await cinemaService.DeleteCinemaAsync(id);
        return NoContent();
    }
}