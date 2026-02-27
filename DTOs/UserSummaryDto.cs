namespace Hive_Movie.DTOs;

public record UserSummaryDto(
    long Id,
    string Email,
    string FirstName,
    string LastName);