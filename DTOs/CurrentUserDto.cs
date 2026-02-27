namespace Hive_Movie.DTOs;

/// <summary>
///     Contains the current user details
/// </summary>
/// <param name="Id">Unique identifier of the current user <example>user_auth_12345</example></param>
/// <param name="Email">Email ID of the current user <example>organizer@hivecinemas.com</example></param>
/// <param name="Roles">List of roles assigned to the current user</param>
public record CurrentUserDetails(
    string Id,
    string Email,
    IReadOnlyList<string> Roles
);