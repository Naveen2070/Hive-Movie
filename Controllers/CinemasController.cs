using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.Cinemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Hive_Movie.Controllers;

/// <summary>
///     Provides endpoints for managing physical cinema locations.
/// </summary>
/// <remarks>
///     This controller exposes public read-only endpoints and restricted management
///     endpoints secured via role-based access control (RBAC).
///     Organizers may only manage, update, or delete cinemas that they created and own.
///     Super administrators have global access and may manage all cinemas across the platform.
/// </remarks>
[Route("api/[controller]")]
[ApiController]
[Tags("Cinemas Management")]
public class CinemasController(ICinemaService cinemaService) : ControllerBase
{
    /// <summary>
    /// Retrieves all physical cinema locations.
    /// </summary>
    /// <remarks>
    /// Returns a complete list of all active cinemas in the system.  
    /// Soft-deleted cinemas are automatically excluded.  
    /// This endpoint is publicly accessible and does not require authentication.
    /// </remarks>
    /// <returns>A list of cinemas currently registered in the system.</returns>
    /// <response code="200">Successfully retrieved the list of cinemas.</response>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CinemaResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await cinemaService.GetAllCinemasAsync());
    }

    /// <summary>
    /// Retrieves a specific cinema by its unique identifier.
    /// </summary>
    /// <remarks>
    /// Returns the details of the requested cinema, including name, location, and approval status.  
    /// Only active (non-soft-deleted) cinemas are returned.  
    /// This endpoint is publicly accessible and does not require authentication.
    /// </remarks>
    /// <param name="id">The unique identifier (UUID v7) of the cinema.</param>
    /// <returns>The requested cinema details.</returns>
    /// <response code="200">The cinema was found and returned successfully.</response>
    /// <response code="404">No cinema exists with the provided ID, or it has been soft-deleted.</response>
    [AllowAnonymous]
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
    /// Restricted to users with roles:
    /// - `ROLE_ORGANIZER`
    /// - `ROLE_SUPER_ADMIN`
    /// 
    /// Creates a new physical cinema record.  
    /// The system automatically generates a sequential UUID v7 and associates the cinema with the creating organizer.
    /// </remarks>
    /// <param name="request">The cinema details to save.</param>
    /// <returns>The newly created cinema.</returns>
    /// <response code="201">The cinema was successfully created.</response>
    /// <response code="400">The request payload failed validation (e.g., missing name or location).</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpPost]
    [ProducesResponseType(typeof(CinemaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateCinemaRequest request)
    {
        var organizerId = User.FindFirst("id")?.Value
            ?? throw new UnauthorizedAccessException("Missing User Id.");

        var cinema = await cinemaService.CreateCinemaAsync(request, organizerId);
        return CreatedAtAction(nameof(GetById), new
        {
            id = cinema.Id
        }, cinema);
    }

    /// <summary>
    /// Updates the approval status of a cinema.
    /// </summary>
    /// <remarks>
    /// Restricted to users with roles:
    /// - `ROLE_SUPER_ADMIN`
    /// 
    /// Allows updating the approval status of a cinema (e.g., Approved, Pending, Rejected).
    /// </remarks>
    /// <param name="id">The UUID v7 of the cinema to update.</param>
    /// <param name="status">The new approval status to assign.</param>
    /// <response code="204">The cinema status was successfully updated.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    /// <response code="404">No cinema exists with the provided ID.</response>
    [Authorize(Roles = "ROLE_SUPER_ADMIN")]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] CinemaApprovalStatus status)
    {
        await cinemaService.UpdateCinemaStatusAsync(id, status);
        return NoContent();
    }

    /// <summary>
    /// Updates a cinema's details.
    /// </summary>
    /// <remarks>
    /// Restricted to users with roles:
    /// - `ROLE_ORGANIZER`
    /// - `ROLE_SUPER_ADMIN`
    /// 
    /// Performs a full replacement (PUT) of the cinema's data.  
    /// All required fields must be provided in the request body.  
    /// </remarks>
    /// <param name="id">The UUID v7 of the cinema to update.</param>
    /// <param name="request">The complete updated cinema details.</param>
    /// <response code="204">The cinema was successfully updated.</response>
    /// <response code="400">The request payload failed validation.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    /// <response code="404">No cinema exists with the provided ID.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCinemaRequest request)
    {
        var currentUser = User.FindFirst("id")?.Value ?? throw new UnauthorizedAccessException();
        var isAdmin = User.IsInRole("ROLE_SUPER_ADMIN");

        await cinemaService.UpdateCinemaAsync(id, request, currentUser, isAdmin);
        return NoContent();
    }

    /// <summary>
    /// Removes a cinema location from the system.
    /// </summary>
    /// <remarks>
    /// Restricted to users with roles:
    /// - `ROLE_ORGANIZER`
    /// - `ROLE_SUPER_ADMIN`
    /// 
    /// Performs a soft-delete on the cinema.  
    /// The record remains in the database for historical auditing but is hidden from standard API queries.  
    /// </remarks>
    /// <param name="id">The UUID v7 of the cinema to delete.</param>
    /// <response code="204">The cinema was successfully deleted.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    /// <response code="404">No cinema exists with the provided ID.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var currentUser = User.FindFirst("id")?.Value ?? throw new UnauthorizedAccessException();
        var isAdmin = User.IsInRole("ROLE_SUPER_ADMIN");

        await cinemaService.DeleteCinemaAsync(id, currentUser, isAdmin);
        return NoContent();
    }
}