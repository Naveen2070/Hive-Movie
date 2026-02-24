namespace Hive_Movie.Models;

public class Cinema : BaseAuditableEntity
{
    public required string OrganizerId { get; set; }

    public required string Name { get; set; }
    public required string Location { get; set; }
    public required string ContactEmail { get; set; }

    public CinemaApprovalStatus ApprovalStatus { get; set; } = CinemaApprovalStatus.Pending;

    public ICollection<Auditorium> Auditoriums { get; set; } = [];
}

public enum CinemaApprovalStatus
{
    Pending,
    Approved,
    Rejected
}