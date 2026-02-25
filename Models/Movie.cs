using System.ComponentModel.DataAnnotations;
namespace Hive_Movie.Models;

public class Movie : BaseAuditableEntity
{
    [MaxLength(255)]
    public required string Title { get; set; }
        
    [MaxLength(500)]
    public required string Description { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime ReleaseDate { get; set; }
        
    [MaxLength(255)]
    public string? PosterUrl { get; set; }
}