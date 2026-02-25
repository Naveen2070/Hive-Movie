using Hive_Movie.Models;
using Hive_Movie.Services.CurrentUser;
using Microsoft.EntityFrameworkCore;
namespace Hive_Movie.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUserService currentUserService)
    : DbContext(options)
{
    public DbSet<Movie> Movies { get; set; }
    public DbSet<Cinema> Cinemas { get; set; }
    public DbSet<Auditorium> Auditoriums { get; set; }
    public DbSet<Showtime> Showtimes { get; set; }
    public DbSet<Ticket> Tickets { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        long? currentUserId = null;
        if (long.TryParse(currentUserService.UserId, out var parsedId))
        {
            currentUserId = parsedId;
        }
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached)
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedBy = currentUserId;
                    entry.Entity.IsActive = true;
                    entry.Entity.IsDeleted = false;
                    break;

                case EntityState.Modified:
                    entry.Property(x => x.CreatedAtUtc).IsModified = false;
                    entry.Property(x => x.CreatedBy).IsModified = false;

                    var isSoftDelete =
                        entry.Property(x => x.IsDeleted).CurrentValue && !entry.Property(x => x.IsDeleted).OriginalValue;

                    if (isSoftDelete)
                    {
                        entry.Entity.DeletedAtUtc = now;
                        entry.Entity.DeletedBy = currentUserId;
                        entry.Entity.IsActive = false;
                    }
                    else
                    {
                        entry.Entity.UpdatedAtUtc = now;
                        entry.Entity.UpdatedBy = currentUserId;
                    }

                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;

                    entry.Entity.IsDeleted = true;
                    entry.Entity.IsActive = false;
                    entry.Entity.DeletedAtUtc = now;
                    entry.Entity.DeletedBy = currentUserId;

                    break;

                case EntityState.Unchanged:
                case EntityState.Detached:
                    // Intentionally ignored
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unexpected EntityState '{entry.State}' encountered in audit processing.");
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global Query Filters to automatically ignore deleted records
        modelBuilder.Entity<Movie>().HasQueryFilter(m => !m.IsDeleted);
        modelBuilder.Entity<Cinema>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<Auditorium>().HasQueryFilter(a => !a.IsDeleted);
        modelBuilder.Entity<Showtime>().HasQueryFilter(s => !s.IsDeleted);
        modelBuilder.Entity<Ticket>().HasQueryFilter(t => !t.IsDeleted);

        // --- ADD THIS JSON MAPPING ---
        // Tell EF Core that LayoutConfiguration is owned by Auditorium and should be stored as JSON
        modelBuilder.Entity<Auditorium>()
            .OwnsOne(a => a.LayoutConfiguration, builder =>
            {
                builder.ToJson(); // Store the parent object as JSON

                // Explicitly tell EF Core that these nested lists also live inside the JSON!
                builder.OwnsMany(l => l.DisabledSeats);
                builder.OwnsMany(l => l.WheelchairSpots);

                // Explicitly tell EF Core to store the Tiers array inside the JSON column too!
                builder.OwnsMany(l => l.Tiers, tierBuilder =>
                {
                    tierBuilder.OwnsMany(t => t.Seats); // The seats inside the tier
                });
            });
        // Mapping for the Ticket
        modelBuilder.Entity<Ticket>(builder =>
        {
            // 1. Tell EF Core to store the money safely
            builder.Property(t => t.TotalAmount).HasPrecision(18, 2);

            // 2. Tell EF Core to store the list of booked seats as a JSON string
            builder.OwnsMany(t => t.ReservedSeats, seatBuilder =>
            {
                seatBuilder.ToJson();
            });
        });
    }
}