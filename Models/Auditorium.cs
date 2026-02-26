using System.ComponentModel.DataAnnotations;
namespace Hive_Movie.Models;

public class Auditorium : BaseAuditableEntity
{
    public Guid CinemaId { get; init; }
    
    [MaxLength(100)]
    public required string Name { get; set; }

    public int MaxRows { get; set; }
    public int MaxColumns { get; set; }

    public AuditoriumLayout LayoutConfiguration { get; set; } = new();

    public Cinema? Cinema { get; init; }
}