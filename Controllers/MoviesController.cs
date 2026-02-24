using Hive_Movie.DTOs;
using Hive_Movie.Services.Movies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hive_Movie.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Movies Catalog")]
public class MoviesController(IMovieService movieService) : ControllerBase
{
    private readonly IMovieService _movieService = movieService;

    /// <summary>
    /// Retrieves the complete catalog of movies.
    /// </summary>
    /// <remarks>
    /// Fetches all active movies currently registered in the system, ordered by release date (newest first).
    /// Soft-deleted movies are automatically filtered out.
    /// </remarks>
    /// <returns>A list of movies.</returns>
    /// <response code="200">Successfully retrieved the movie catalog.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MovieResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var movies = await _movieService.GetAllMoviesAsync();
        return Ok(movies);
    }

    /// <summary>
    /// Retrieves a specific movie by its unique identifier.
    /// </summary>
    /// <param name="id">The UUID v7 of the movie to retrieve.</param>
    /// <returns>The requested movie details.</returns>
    /// <response code="200">The movie was found and returned successfully.</response>
    /// <response code="404">No movie exists with the provided ID, or it has been deleted.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MovieResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var movie = await _movieService.GetMovieByIdAsync(id);
        return Ok(movie);
    }

    /// <summary>
    /// Registers a new movie in the catalog.
    /// </summary>
    /// <remarks>
    /// Creates a new movie record. The system will automatically generate a sequential UUID v7 
    /// and populate the audit fields (CreatedAt, CreatedBy).
    /// </remarks>
    /// <param name="request">The movie details to save.</param>
    /// <returns>The newly created movie.</returns>
    /// <response code="201">The movie was successfully created.</response>
    /// <response code="400">The request payload failed validation (e.g., missing title, invalid duration).</response>
    [Authorize(Roles = "ROLE_ORGANIZER,ROLE_SUPER_ADMIN")]
    [HttpPost]
    [ProducesResponseType(typeof(MovieResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateMovieRequest request)
    {
        var movie = await _movieService.CreateMovieAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = movie.Id }, movie);
    }

    /// <summary>
    /// Updates an existing movie's details.
    /// </summary>
    /// <remarks>
    /// Performs a full replacement (PUT) of the movie's data. All fields in the request body 
    /// must be provided, even if they are not changing.
    /// </remarks>
    /// <param name="id">The UUID v7 of the movie to update.</param>
    /// <param name="request">The complete updated movie details.</param>
    /// <response code="204">The movie was successfully updated.</response>
    /// <response code="400">The request payload failed validation.</response>
    /// <response code="404">No movie exists with the provided ID.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMovieRequest request)
    {
        await _movieService.UpdateMovieAsync(id, request);
        return NoContent();
    }

    /// <summary>
    /// Removes a movie from the catalog.
    /// </summary>
    /// <remarks>
    /// Performs a soft-delete on the movie. The record will remain in the database for historical 
    /// auditing but will be immediately hidden from all catalog queries.
    /// </remarks>
    /// <param name="id">The UUID v7 of the movie to delete.</param>
    /// <response code="204">The movie was successfully deleted.</response>
    /// <response code="404">No movie exists with the provided ID.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _movieService.DeleteMovieAsync(id);
        return NoContent();
    }
}