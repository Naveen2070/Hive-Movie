using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
namespace Hive_Movie.Models;

// This is NOT a database table. It is a JSON object stored INSIDE the Auditorium table.
public class AuditoriumLayout
{
    // Coordinates for seats that don't exist (e.g., aisles or missing corners)
    public IReadOnlyCollection<SeatCoordinate> DisabledSeats { get; init; } = new List<SeatCoordinate>();

    // Coordinates for wheelchair-accessible spots
    public IReadOnlyCollection<SeatCoordinate> WheelchairSpots { get; init; } = new List<SeatCoordinate>();
    
    // The pricing tiers for this specific room
    public IReadOnlyCollection<SeatTier> Tiers { get; init; } = new List<SeatTier>();
}

public class SeatCoordinate
{
    public int Row { get; init; }
    public int Col { get; init; }
}

// Represents a group of seats that cost extra (or less)
public class SeatTier
{
    [MaxLength(100)]
    public required string TierName { get; init; } 
    
    [Precision(18, 2)]
    public decimal PriceSurcharge { get; init; } 
    public IReadOnlyCollection<SeatCoordinate> Seats { get; init; } = [];
}