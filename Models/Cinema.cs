namespace Hive_Movie.Models
{
    public class Cinema : BaseAuditableEntity
    {
        public required string Name { get; set; }
        public required string Location { get; set; }

        public ICollection<Auditorium> Auditoriums { get; set; } = new List<Auditorium>();
    }
}
