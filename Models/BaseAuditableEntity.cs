namespace Hive_Movie.Models;

public abstract class BaseAuditableEntity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    // Creation Audit
    public DateTime CreatedAtUtc { get; set; }
    public long? CreatedBy { get; set; }

    // Modification Audit
    public DateTime? UpdatedAtUtc { get; set; }
    public long? UpdatedBy { get; set; }

    // Soft Delete Audit
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAtUtc { get; set; }
    public long? DeletedBy { get; set; }
}