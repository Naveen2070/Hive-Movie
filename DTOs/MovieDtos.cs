using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
namespace Hive_Movie.DTOs;

/// <summary>
/// The payload required to register a new movie in the catalog.
/// </summary>
/// <param name="Title">The official, un-translated title of the movie. <example>Inception</example></param>
/// <param name="Description">A brief synopsis of the movie plot. <example>A thief who steals corporate secrets through the use of dream-sharing technology is given the inverse task of planting an idea into the mind of a C.E.O.</example></param>
/// <param name="DurationMinutes">The total runtime of the movie in minutes. <example>148</example></param>
/// <param name="ReleaseDate">The global premiere date (UTC). <example>2010-07-16T00:00:00Z</example></param>
/// <param name="PosterUrl">An optional fully qualified URL to the movie's promotional poster. <example>https://example.com/posters/inception.jpg</example></param>
public record CreateMovieRequest(
    [Required][MaxLength(200)] string Title,
    [Required][MaxLength(2000)] string Description,
    [Range(1, 600)][DefaultValue(120)] int DurationMinutes,
    [Required] DateTime ReleaseDate,
    [Url][MaxLength(1000)] string? PosterUrl);

/// <summary>
/// The payload required to update an existing movie. All fields must be provided.
/// </summary>
/// <param name="Title">The updated title of the movie. <example>Inception (Director's Cut)</example></param>
/// <param name="Description">The updated synopsis. <example>The expanded director's cut featuring an additional 15 minutes of dream sequences.</example></param>
/// <param name="DurationMinutes">The updated runtime in minutes. <example>163</example></param>
/// <param name="ReleaseDate">The updated release date (UTC). <example>2010-07-16T00:00:00Z</example></param>
/// <param name="PosterUrl">The updated poster URL. <example>https://example.com/posters/inception_dc.jpg</example></param>
public record UpdateMovieRequest(
    [Required][MaxLength(200)] string Title,
    [Required][MaxLength(2000)] string Description,
    [Range(1, 600)][DefaultValue(120)] int DurationMinutes,
    [Required] DateTime ReleaseDate,
    [Url][MaxLength(1000)] string? PosterUrl);

/// <summary>
/// Represents a movie record returned from the catalog.
/// </summary>
/// <param name="Id">The unique system identifier for the movie. <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example></param>
/// <param name="Title">The official title of the movie. <example>Inception</example></param>
/// <param name="Description">A brief synopsis of the movie plot. <example>A thief who steals corporate secrets through the use of dream-sharing technology...</example></param>
/// <param name="DurationMinutes">The total runtime of the movie in minutes. <example>148</example></param>
/// <param name="ReleaseDate">The global premiere date (UTC). <example>2010-07-16T00:00:00Z</example></param>
/// <param name="PosterUrl">A fully qualified URL to the movie's promotional poster, if available. <example>https://example.com/posters/inception.jpg</example></param>
public record MovieResponse(
    Guid Id,
    string Title,
    string Description,
    int DurationMinutes,
    DateTime ReleaseDate,
    string? PosterUrl);