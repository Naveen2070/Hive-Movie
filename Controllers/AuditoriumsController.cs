using Hive_Movie.DTOs;
using Hive_Movie.Services.Auditoriums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Hive_Movie.Controllers;

/// <summary>
/// Provides endpoints for managing cinema auditoriums and their seating layouts.
/// </summary>
/// <remarks>
/// This controller exposes public read-only endpoints and restricted management 
/// endpoints secured via role-based access control (RBAC).
/// 
/// Organizers may only manage auditoriums belonging to cinemas they own.
/// Super administrators may manage all auditoriums.
/// </remarks>
[Route("api/[controller]")]
[ApiController]
[Tags("Auditoriums Management")]
public class AuditoriumsController(IAuditoriumService auditoriumService) : ControllerBase
{
    /// <summary>
    /// Retrieves all auditoriums across all cinemas.
    /// </summary>
    /// <remarks>
    /// Returns all active auditoriums in the system. Soft-deleted records are automatically excluded.  
    /// This endpoint is publicly accessible and does not require authentication.
    /// </remarks>
    /// <returns>A list of all active auditoriums with their seating layouts.</returns>
    /// <response code="200">Successfully retrieved the list of auditoriums.</response>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AuditoriumResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await auditoriumService.GetAllAuditoriumsAsync());
    }

    /// <summary>
    /// Retrieves all auditoriums for a specific cinema.
    /// </summary>
    /// <remarks>
    /// Useful for displaying available screens when a user selects a specific cinema location.  
    /// Only active (non-soft-deleted) auditoriums are returned.  
    /// This endpoint is publicly accessible and does not require authentication.
    /// </remarks>
    /// <param name="cinemaId">The unique identifier (UUID v7) of the parent cinema.</param>
    /// <returns>A list of auditoriums belonging to the specified cinema.</returns>
    /// <response code="200">Successfully retrieved the auditoriums for the given cinema.</response>
    [AllowAnonymous]
    [HttpGet("cinema/{cinemaId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AuditoriumResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCinemaId(Guid cinemaId)
    {
        return Ok(await auditoriumService.GetAuditoriumsByCinemaIdAsync(cinemaId));
    }

    /// <summary>
    /// Retrieves a specific auditorium by its unique identifier.
    /// </summary>
    /// <remarks>
    /// Returns the details of the specified auditorium, including its seating layout.  
    /// Soft-deleted auditoriums are not returned.  
    /// This endpoint is publicly accessible and does not require authentication.
    /// </remarks>
    /// <param name="id">The unique identifier (UUID v7) of the auditorium.</param>
    /// <returns>The auditorium details and its seating configuration.</returns>
    /// <response code="200">The auditorium was found and returned successfully.</response>
    /// <response code="404">No auditorium exists with the provided ID, or it has been soft-deleted.</response>
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditoriumResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        return Ok(await auditoriumService.GetAuditoriumByIdAsync(id));
    }

    /// <summary>
    /// Creates a new auditorium and its seating layout.
    /// </summary>
    /// <remarks>
    /// Restricted to users with roles:
    /// - `ROLE_ORGANIZER`
    /// - `ROLE_SUPER_ADMIN`
    /// 
    /// Organizers may only create auditoriums for cinemas they own.
    /// The seating layout is fully validated to ensure coordinates remain within
    /// the specified grid bounds (`MaxRows` and `MaxColumns`).
    /// </remarks>
    /// <param name="request">The auditorium dimensions and seating layout configuration.</param>
    /// <returns>The newly created auditorium.</returns>
    /// <response code="201">The auditorium was successfully created.</response>
    /// <response code="400">The request payload failed validation.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    /// <response code="404">The specified parent CinemaId does not exist.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpPost]
    [ProducesResponseType(typeof(AuditoriumResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateAuditoriumRequest request)
    {
        var currentUser = User.FindFirst("id")?.Value ?? throw new UnauthorizedAccessException();
        var isAdmin = User.IsInRole("ROLE_SUPER_ADMIN");

        var auditorium = await auditoriumService.CreateAuditoriumAsync(request, currentUser, isAdmin);

        return CreatedAtAction(nameof(GetById), new
        {
            id = auditorium.Id
        }, auditorium);
    }

    /// <summary>
    /// Updates an existing auditorium and replaces its seating layout.
    /// </summary>
    /// <remarks>
    /// Restricted to users with roles:
    /// - `ROLE_ORGANIZER`
    /// - `ROLE_SUPER_ADMIN`
    /// 
    /// Performs a full replacement (PUT) of the auditorium entity, including the nested
    /// layout configuration. All seat coordinates are re-validated against the updated dimensions.
    /// </remarks>
    /// <param name="id">The unique identifier of the auditorium.</param>
    /// <param name="request">The complete updated auditorium configuration.</param>
    /// <response code="204">The auditorium was successfully updated.</response>
    /// <response code="400">The request payload failed validation.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    /// <response code="404">No auditorium exists with the provided ID.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAuditoriumRequest request)
    {
        var currentUser = User.FindFirst("id")?.Value ?? throw new UnauthorizedAccessException();
        var isAdmin = User.IsInRole("ROLE_SUPER_ADMIN");

        await auditoriumService.UpdateAuditoriumAsync(id, request, currentUser, isAdmin);
        return NoContent();
    }

    /// <summary>
    /// Soft-deletes an auditorium.
    /// </summary>
    /// <remarks>
    /// Restricted to users with roles:
    /// - `ROLE_ORGANIZER`
    /// - `ROLE_SUPER_ADMIN`
    /// 
    /// The record remains stored for auditing and historical integrity but is excluded
    /// from standard query results.
    /// </remarks>
    /// <param name="id">The unique identifier of the auditorium.</param>
    /// <response code="204">The auditorium was successfully deleted.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    /// <response code="404">No auditorium exists with the provided ID.</response>
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

        await auditoriumService.DeleteAuditoriumAsync(id, currentUser, isAdmin);
        return NoContent();
    }
}