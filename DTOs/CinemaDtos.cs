using Hive_Movie.Models;
using System;
using System.ComponentModel.DataAnnotations;

namespace Hive_Movie.DTOs;

/// <summary>
/// The payload required to register a new physical cinema location.
/// </summary>
/// <param name="Name">The official name of the cinema multiplex (e.g., "Hive Multiplex Downtown").</param>
/// <param name="Location">The physical address or geographical location of the cinema.</param>
/// <param name="ContactEmail">The email address of the contact person of that cinema.</param>
public record CreateCinemaRequest(
    [Required][MaxLength(200)] string Name,
    [Required][MaxLength(500)] string Location,
    [Required][EmailAddress]string ContactEmail);

/// <summary>
/// The payload required to update an existing cinema location. All fields must be provided.
/// </summary>
/// <param name="Name">The updated name of the cinema.</param>
/// <param name="Location">The updated physical address of the cinema.</param>
public record UpdateCinemaRequest(
    [Required][MaxLength(200)] string Name,
    [Required][MaxLength(500)] string Location);

/// <summary>
/// Represents a cinema multiplex record returned from the system.
/// </summary>
/// <param name="Id">The unique system identifier for the cinema.</param>
/// <param name="Name">The official name of the cinema.</param>
/// <param name="Location">The physical address of the cinema.</param>
public record CinemaResponse(
    Guid Id,
    string Name,
    string Location)
{
    public static CinemaResponse MapToResponse(Cinema cinema)
{
    return new CinemaResponse(
        cinema.Id,
        cinema.Name,
        cinema.Location
    );
}
}