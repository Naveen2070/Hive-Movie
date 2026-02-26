using System.ComponentModel.DataAnnotations;
namespace Hive_Movie.Models;

public class Cinema : BaseAuditableEntity
{
    [MaxLength(255)]
    public required string OrganizerId { get; init; }

    [MaxLength(150)]
    public required string Name { get; set; }
    
    [MaxLength(500)]
    public required string Location { get; set; }
    
    [MaxLength(255)]
    public required string ContactEmail { get; init; }

    public CinemaApprovalStatus ApprovalStatus { get; set; } = CinemaApprovalStatus.Pending;

    public ICollection<Auditorium> Auditoriums { get; init; } = [];
}

public enum CinemaApprovalStatus
{
    Pending,
    Approved,
    Rejected
}