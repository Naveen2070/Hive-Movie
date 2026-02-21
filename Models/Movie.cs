namespace Hive_Movie.Models
{
    public class Movie : BaseAuditableEntity
    {
        public required string Title { get; set; }
        public required string Description { get; set; }
        public int DurationMinutes { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string? PosterUrl { get; set; }
    }
}
