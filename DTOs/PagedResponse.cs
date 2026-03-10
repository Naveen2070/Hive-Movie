namespace Hive_Movie.DTOs;

/// <summary>
///     A standardized pagination wrapper that matches the Spring Data JPA format expected by the frontend.
/// </summary>
public record PagedResponse<T>(
    IEnumerable<T> Content,
    int PageNumber,
    int PageSize,
    int TotalElements,
    int TotalPages,
    bool IsLast
);