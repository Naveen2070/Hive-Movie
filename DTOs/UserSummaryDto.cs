namespace Hive_Movie.DTOs;

public record UserSummaryDto(
    long Id,
    string FullName,
    string Email);