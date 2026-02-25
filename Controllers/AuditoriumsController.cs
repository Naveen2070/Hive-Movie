using Hive_Movie.DTOs;
using Hive_Movie.Services.Auditoriums;
using Microsoft.AspNetCore.Mvc;

namespace Hive_Movie.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Auditoriums Management")]
public class AuditoriumsController(IAuditoriumService auditoriumService) : ControllerBase
{
    /// <summary>
    /// Retrieves all auditoriums across all cinemas.
    /// </summary>
    /// <remarks>
    /// Fetches a complete list of all active auditoriums in the system. 
    /// Soft-deleted auditoriums are automatically excluded.
    /// </remarks>
    /// <returns>A comprehensive list of auditoriums.</returns>
    /// <response code="200">Successfully retrieved the auditoriums.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AuditoriumResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await auditoriumService.GetAllAuditoriumsAsync());
    }

    /// <summary>
    /// Retrieves all auditoriums for a specific physical cinema.
    /// </summary>
    /// <remarks>
    /// Useful for displaying the available screens when a user selects a specific cinema location.
    /// </remarks>
    /// <param name="cinemaId">The UUID v7 of the parent cinema.</param>
    /// <returns>A list of auditoriums belonging to the specified cinema.</returns>
    /// <response code="200">Successfully retrieved the filtered auditoriums.</response>
    [HttpGet("cinema/{cinemaId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AuditoriumResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCinemaId(Guid cinemaId)
    {
        return Ok(await auditoriumService.GetAuditoriumsByCinemaIdAsync(cinemaId));
    }

    /// <summary>
    /// Retrieves a specific auditorium by ID, including its seating layout.
    /// </summary>
    /// <param name="id">The UUID v7 of the auditorium to retrieve.</param>
    /// <returns>The requested auditorium details and JSON layout.</returns>
    /// <response code="200">The auditorium was found and returned successfully.</response>
    /// <response code="404">No auditorium exists with the provided ID, or it has been deleted.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditoriumResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        return Ok(await auditoriumService.GetAuditoriumByIdAsync(id));
    }

    /// <summary>
    /// Registers a new auditorium and its physical seating layout.
    /// </summary>
    /// <remarks>
    /// Creates a new auditorium. The payload is validated to ensure that no `DisabledSeats` 
    /// or `WheelchairSpots` coordinates fall outside the bounds of `MaxRows` and `MaxColumns`.
    /// </remarks>
    /// <param name="request">The auditorium dimensions and JSON layout to save.</param>
    /// <returns>The newly created auditorium.</returns>
    /// <response code="201">The auditorium was successfully created.</response>
    /// <response code="400">The request failed cross-property Fluent Validation constraints.</response>
    /// <response code="404">The specified parent CinemaId does not exist.</response>
    [HttpPost]
    [ProducesResponseType(typeof(AuditoriumResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateAuditoriumRequest request)
    {
        var auditorium = await auditoriumService.CreateAuditoriumAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = auditorium.Id }, auditorium);
    }

    /// <summary>
    /// Updates an auditorium and alters its physical seating layout.
    /// </summary>
    /// <remarks>
    /// Performs a full replacement (PUT) of the auditorium's data, including the nested JSON layout. 
    /// All grid coordinates will be re-validated against the newly provided room dimensions.
    /// </remarks>
    /// <param name="id">The UUID v7 of the auditorium to update.</param>
    /// <param name="request">The complete updated auditorium details.</param>
    /// <response code="204">The auditorium was successfully updated.</response>
    /// <response code="400">The request failed cross-property Fluent Validation constraints.</response>
    /// <response code="404">No auditorium exists with the provided ID.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAuditoriumRequest request)
    {
        await auditoriumService.UpdateAuditoriumAsync(id, request);
        return NoContent();
    }

    /// <summary>
    /// Soft-deletes an auditorium.
    /// </summary>
    /// <remarks>
    /// The record remains in the database for historical auditing but is hidden from standard API queries.
    /// </remarks>
    /// <param name="id">The UUID v7 of the auditorium to delete.</param>
    /// <response code="204">The auditorium was successfully deleted.</response>
    /// <response code="404">No auditorium exists with the provided ID.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await auditoriumService.DeleteAuditoriumAsync(id);
        return NoContent();
    }
}