using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Hive_Movie.DTOs;

/// <summary>
/// The payload required to register a new movie in the catalog.
/// </summary>
/// <param name="Title">The official, un-translated title of the movie.</param>
/// <param name="Description">A brief synopsis of the movie plot.</param>
/// <param name="DurationMinutes">The total runtime of the movie in minutes.</param>
/// <param name="ReleaseDate">The global premiere date (UTC).</param>
/// <param name="PosterUrl">An optional fully qualified URL to the movie's promotional poster.</param>
public record CreateMovieRequest(
    [Required][MaxLength(200)] string Title,
    [Required][MaxLength(2000)] string Description,
    [Range(1, 600)][DefaultValue(120)] int DurationMinutes,
    [Required] DateTime ReleaseDate,
    [Url][MaxLength(1000)] string? PosterUrl);

/// <summary>
/// The payload required to update an existing movie. All fields must be provided.
/// </summary>
/// <param name="Title">The updated title of the movie.</param>
/// <param name="Description">The updated synopsis.</param>
/// <param name="DurationMinutes">The updated runtime in minutes.</param>
/// <param name="ReleaseDate">The updated release date (UTC).</param>
/// <param name="PosterUrl">The updated poster URL.</param>
public record UpdateMovieRequest(
    [Required][MaxLength(200)] string Title,
    [Required][MaxLength(2000)] string Description,
    [Range(1, 600)][DefaultValue(120)] int DurationMinutes,
    [Required] DateTime ReleaseDate,
    [Url][MaxLength(1000)] string? PosterUrl);

/// <summary>
/// Represents a movie record returned from the catalog.
/// </summary>
/// <param name="Id">The unique system identifier for the movie.</param>
/// <param name="Title">The official title of the movie.</param>
/// <param name="Description">A brief synopsis of the movie plot.</param>
/// <param name="DurationMinutes">The total runtime of the movie in minutes.</param>
/// <param name="ReleaseDate">The global premiere date (UTC).</param>
/// <param name="PosterUrl">A fully qualified URL to the movie's promotional poster, if available.</param>
public record MovieResponse(
    Guid Id,
    string Title,
    string Description,
    int DurationMinutes,
    DateTime ReleaseDate,
    string? PosterUrl);
