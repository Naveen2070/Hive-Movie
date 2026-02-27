using Hive_Movie.DTOs;
using Hive_Movie.Services.Movies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Hive_Movie.Controllers;

/// <summary>
/// Provides endpoints for managing and browsing the movie catalog.
/// </summary>
/// <remarks>
/// This controller exposes public read-only catalog endpoints and restricted
/// management endpoints secured via role-based access control (RBAC).
/// 
/// Restricted to users with roles:
/// - `ROLE_ORGANIZER`
/// - `ROLE_SUPER_ADMIN`
/// 
/// These users may create, update, or delete movies within the global catalog.
/// </remarks>
[Route("api/[controller]")]
[ApiController]
[Tags("Movies Catalog")]
public class MoviesController(IMovieService movieService) : ControllerBase
{
    /// <summary>
    /// Retrieves the complete catalog of movies.
    /// </summary>
    /// <remarks>
    /// Returns all active (non-soft-deleted) movies currently registered in the system.  
    /// Results are ordered by release date, with the newest movies first.  
    /// This endpoint is publicly accessible and does not require authentication.
    /// </remarks>
    /// <returns>A list of movies currently available in the catalog.</returns>
    /// <response code="200">Successfully retrieved the movie catalog.</response>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MovieResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var movies = await movieService.GetAllMoviesAsync();
        return Ok(movies);
    }

    /// <summary>
    /// Retrieves a specific movie by its unique identifier.
    /// </summary>
    /// <remarks>
    /// Returns the details of the requested movie, including metadata such as title, duration, and release date.  
    /// Only active (non-soft-deleted) movies are returned.  
    /// This endpoint is publicly accessible and does not require authentication.
    /// </remarks>
    /// <param name="id">The unique identifier (UUID v7) of the movie.</param>
    /// <returns>The details of the specified movie.</returns>
    /// <response code="200">The movie was found and returned successfully.</response>
    /// <response code="404">No movie exists with the provided ID, or it has been soft-deleted.</response>
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MovieResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var movie = await movieService.GetMovieByIdAsync(id);
        return Ok(movie);
    }

    /// <summary>
    /// Registers a new movie in the catalog.
    /// </summary>
    /// <remarks>
    /// Restricted to users with roles:
    /// - `ROLE_ORGANIZER`
    /// - `ROLE_SUPER_ADMIN`
    /// 
    /// Creates a new movie record in the system.  
    /// The system automatically generates a sequential UUID v7
    /// and populates audit metadata such as creation timestamps.
    /// </remarks>
    /// <param name="request">The movie details to register.</param>
    /// <returns>The newly created movie.</returns>
    /// <response code="201">The movie was successfully created.</response>
    /// <response code="400">The request payload failed validation.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpPost]
    [ProducesResponseType(typeof(MovieResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateMovieRequest request)
    {
        var movie = await movieService.CreateMovieAsync(request);
        return CreatedAtAction(nameof(GetById), new
        {
            id = movie.Id
        }, movie);
    }

    /// <summary>
    /// Updates an existing movie's details.
    /// </summary>
    /// <remarks>
    /// Restricted to users with roles:
    /// - `ROLE_ORGANIZER`
    /// - `ROLE_SUPER_ADMIN`
    /// 
    /// Performs a full replacement (HTTP PUT) of the movie's data.
    /// All properties must be provided in the request body.
    /// </remarks>
    /// <param name="id">The unique identifier of the movie.</param>
    /// <param name="request">The complete updated movie details.</param>
    /// <response code="204">The movie was successfully updated.</response>
    /// <response code="400">The request payload failed validation.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    /// <response code="404">No movie exists with the provided ID.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMovieRequest request)
    {
        await movieService.UpdateMovieAsync(id, request);
        return NoContent();
    }

    /// <summary>
    /// Soft-deletes a movie from the catalog.
    /// </summary>
    /// <remarks>
    /// Restricted to users with roles:
    /// - `ROLE_ORGANIZER`
    /// - `ROLE_SUPER_ADMIN`
    /// 
    /// Performs a soft-delete operation.  
    /// The record remains stored for historical auditing but is excluded
    /// from all standard catalog queries.
    /// </remarks>
    /// <param name="id">The unique identifier of the movie.</param>
    /// <response code="204">The movie was successfully deleted.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user does not have sufficient permissions.</response>
    /// <response code="404">No movie exists with the provided ID.</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await movieService.DeleteMovieAsync(id);
        return NoContent();
    }
}