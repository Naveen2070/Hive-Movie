namespace Hive_Movie.Models
{
    // This is NOT a database table. It is a JSON object stored INSIDE the Auditorium table.
    public class AuditoriumLayout
    {
        // Coordinates for seats that don't exist (e.g., aisles or missing corners)
        public List<SeatCoordinate> DisabledSeats { get; set; } = new();

        // Coordinates for wheelchair-accessible spots
        public List<SeatCoordinate> WheelchairSpots { get; set; } = new();
    }

    public class SeatCoordinate
    {
        public int Row { get; set; }
        public int Col { get; set; }
    }
}
