using Hive_Movie.Models;
using System.ComponentModel.DataAnnotations;
namespace Hive_Movie.DTOs;

/// <summary>
/// The payload required to register a new physical cinema location.
/// </summary>
/// <param name="Name">The official name of the cinema multiplex (e.g., "Hive Multiplex Downtown"). <example>Hive Multiplex Downtown</example></param>
/// <param name="Location">The physical address or geographical location of the cinema. <example>123 Entertainment Blvd, Tech City, CA 90210</example></param>
/// <param name="ContactEmail">The email address of the contact person of that cinema. <example>contact@hivecinemas.com</example></param>
public record CreateCinemaRequest(
    [Required][MaxLength(200)] string Name,
    [Required][MaxLength(500)] string Location,
    [Required][EmailAddress] string ContactEmail);

/// <summary>
/// The payload required to update an existing cinema location. All fields must be provided.
/// </summary>
/// <param name="Name">The updated name of the cinema. <example>Hive Cinema Central</example></param>
/// <param name="Location">The updated physical address of the cinema. <example>456 Updated Ave, Innovation District</example></param>
public record UpdateCinemaRequest(
    [Required][MaxLength(200)] string Name,
    [Required][MaxLength(500)] string Location);

/// <summary>
/// Represents a cinema multiplex record returned from the system.
/// </summary>
/// <param name="Id">The unique system identifier for the cinema. <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example></param>
/// <param name="Name">The official name of the cinema. <example>Hive Multiplex Downtown</example></param>
/// <param name="Location">The physical address of the cinema. <example>123 Entertainment Blvd, Tech City, CA 90210</example></param>
/// <param name="ApprovalStatus">The approval status of the cinema. <example>Approved</example></param>
public record CinemaResponse(
    Guid Id,
    string Name,
    string Location,
    string ApprovalStatus)
{
    public static CinemaResponse MapToResponse(Cinema cinema)
    {
        return new CinemaResponse(
            cinema.Id,
            cinema.Name,
            cinema.Location,
            cinema.ApprovalStatus.ToString()
        );
    }
}