using System.ComponentModel.DataAnnotations;
namespace Hive_Movie.DTOs;

/// <summary>
/// The payload required to reserve a group of seats for a specific showtime.
/// </summary>
/// <param name="Seats">A list of seat coordinates to reserve. Must contain at least one seat.</param>
public record ReserveSeatsRequest(
    [Required, MinLength(1, ErrorMessage = "You must select at least one seat.")]
    List<SeatCoordinateDto> Seats
);

/// <summary>
/// Represents the 2D grid coordinates of a specific seat.
/// </summary>
/// <param name="Row">The zero-based row index.</param>
/// <param name="Col">The zero-based column index.</param>
public record SeatCoordinateDto(
    [Range(0, 1000)] int Row,
    [Range(0, 1000)] int Col
);

/// <summary>
/// The complete layout and status of every seat in the auditorium for a specific showtime.
/// </summary>
/// <param name="MovieTitle">The title of the movie playing.</param>
/// <param name="CinemaName">The name of the physical cinema building.</param>
/// <param name="AuditoriumName">The name of the room/screen.</param>
/// <param name="MaxRows">The total number of rows in the grid.</param>
/// <param name="MaxColumns">The total number of columns in the grid.</param>
/// <param name="SeatMap">A flat list representing the status of every individual seat.</param>
public record ShowtimeSeatMapResponse(
    string MovieTitle,
    string CinemaName,
    string AuditoriumName,
    int MaxRows,
    int MaxColumns,
    List<SeatStatusDto> SeatMap
);

/// <summary>
/// The availability status of a single seat.
/// </summary>
/// <param name="Row">The zero-based row index.</param>
/// <param name="Col">The zero-based column index.</param>
/// <param name="Status">The current state of the seat (e.g., Available, Reserved, Sold).</param>
public record SeatStatusDto(
    int Row,
    int Col,
    string Status
);

/// <summary>
/// The payload required to create a new showtime for a specific movie in a specific auditorium.
/// </summary>
/// <param name="MovieId">The unique identifier of the movie being screened.</param>
/// <param name="AuditoriumId">The unique identifier of the auditorium where the movie will be shown.</param>
/// <param name="StartTimeUtc">The scheduled start time of the showtime in UTC.</param>
/// <param name="BasePrice">The base ticket price before applying any seat tier surcharges.</param>
public record CreateShowtimeRequest(
    Guid MovieId,
    Guid AuditoriumId,
    DateTime StartTimeUtc,
    [Range(0, 1000)] decimal BasePrice
);

/// <summary>
/// The payload required to update an existing showtime.
/// </summary>
/// <param name="StartTimeUtc">The updated scheduled start time in UTC.</param>
/// <param name="BasePrice">The updated base ticket price before applying seat tier surcharges.</param>
public record UpdateShowtimeRequest(
    DateTime StartTimeUtc,
    [Range(0, 1000)] decimal BasePrice
);

/// <summary>
/// Represents the details of a showtime returned to the client.
/// </summary>
/// <param name="Id">The unique identifier of the showtime.</param>
/// <param name="MovieId">The unique identifier of the associated movie.</param>
/// <param name="AuditoriumId">The unique identifier of the auditorium where the showtime takes place.</param>
/// <param name="StartTimeUtc">The scheduled start time of the showtime in UTC.</param>
/// <param name="BasePrice">The base ticket price before seat tier adjustments.</param>
public record ShowtimeResponse(
    Guid Id,
    Guid MovieId,
    Guid AuditoriumId,
    DateTime StartTimeUtc,
    decimal BasePrice
);