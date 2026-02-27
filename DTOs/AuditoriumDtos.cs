namespace Hive_Movie.DTOs;

/// <summary>
/// The payload required to register a new auditorium and its physical seating layout.
/// </summary>
/// <param name="CinemaId">The UUID v7 of the physical cinema this auditorium belongs to. <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example></param>
/// <param name="Name">The name or number of the room (e.g., "IMAX Screen 1"). <example>IMAX Screen 1</example></param>
/// <param name="MaxRows">The total number of rows in the seating grid. <example>15</example></param>
/// <param name="MaxColumns">The total number of columns in the seating grid. <example>20</example></param>
/// <param name="Layout">The JSON configuration defining disabled seats and wheelchair spots.</param>
public record CreateAuditoriumRequest(
    Guid CinemaId,
    string Name,
    int MaxRows,
    int MaxColumns,
    AuditoriumLayoutDto Layout);

/// <summary>
/// The payload required to update an existing auditorium. All fields must be provided.
/// </summary>
/// <param name="Name">The updated name of the room. <example>Standard Screen 2</example></param>
/// <param name="MaxRows">The updated total number of rows. <example>12</example></param>
/// <param name="MaxColumns">The updated total number of columns. <example>15</example></param>
/// <param name="Layout">The fully updated JSON seating configuration.</param>
public record UpdateAuditoriumRequest(
    string Name,
    int MaxRows,
    int MaxColumns,
    AuditoriumLayoutDto Layout);

/// <summary>
/// Represents the physical abnormalities in the auditorium's seating grid.
/// </summary>
/// <param name="DisabledSeats">A list of grid coordinates where no physical seat exists (e.g., aisles or pillars).</param>
/// <param name="WheelchairSpots">A list of grid coordinates designated specifically for wheelchair access.</param>
/// <param name="Tiers">A list of tier for a specific group of seats.</param>
public record AuditoriumLayoutDto(
    List<SeatCoordinateDto> DisabledSeats,
    List<SeatCoordinateDto> WheelchairSpots,
    List<SeatTierDto> Tiers);

/// <summary>
/// Represents a pricing tier for a specific group of seats.
/// </summary>
/// <param name="TierName">e.g., "VIP Recliners" <example>VIP Recliners</example></param>
/// <param name="PriceSurcharge">The extra cost added to the Showtime base price. <example>5.50</example></param>
/// <param name="Seats">The specific coordinates belonging to this tier.</param>
public record SeatTierDto(
    string TierName,
    decimal PriceSurcharge,
    List<SeatCoordinateDto> Seats
);

/// <summary>
/// Represents an auditorium record returned from the system.
/// </summary>
/// <param name="Id">The unique system identifier for the auditorium. <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example></param>
/// <param name="CinemaId">The UUID v7 of the physical cinema it belongs to. <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example></param>
/// <param name="Name">The name or number of the room. <example>IMAX Screen 1</example></param>
/// <param name="MaxRows">The total number of rows in the grid. <example>15</example></param>
/// <param name="MaxColumns">The total number of columns in the grid. <example>20</example></param>
/// <param name="Layout">The nested JSON configuration of the seating layout.</param>
public record AuditoriumResponse(
    Guid Id,
    Guid CinemaId,
    string Name,
    int MaxRows,
    int MaxColumns,
    AuditoriumLayoutDto Layout);