using Hive_Movie.Models;
using Hive_Movie.Services.CurrentUser;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace Hive_Movie.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly ICurrentUserService _currentUserService;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            ICurrentUserService currentUserService) : base(options)
        {
            _currentUserService = currentUserService;
        }

        public DbSet<Movie> Movies { get; set; }
        public DbSet<Cinema> Cinemas { get; set; }
        public DbSet<Auditorium> Auditoriums { get; set; }
        public DbSet<Showtime> Showtimes { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            long? currentUserId = null;
            if (long.TryParse(_currentUserService.UserId, out var parsedId))
            {
                currentUserId = parsedId;
            }
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAtUtc = now;
                        entry.Entity.CreatedBy = currentUserId;
                        entry.Entity.IsActive = true;
                        entry.Entity.IsDeleted = false;
                        break;

                    case EntityState.Modified:
                        if (entry.Entity.IsDeleted && !entry.Property(x => x.IsDeleted).OriginalValue)
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
                        // Intercept hard deletes and convert them to soft deletes
                        entry.State = EntityState.Modified;
                        entry.Entity.IsDeleted = true;
                        entry.Entity.DeletedAtUtc = now;
                        entry.Entity.DeletedBy = currentUserId;
                        entry.Entity.IsActive = false;
                        break;
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
        }
    }
}
