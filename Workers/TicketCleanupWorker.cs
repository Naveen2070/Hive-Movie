using Hive_Movie.Data;
using Hive_Movie.Engine;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
namespace Hive_Movie.Workers;

public class TicketCleanupWorker(
    IServiceProvider serviceProvider,
    ILogger<TicketCleanupWorker> logger,
    IMemoryCache cache) : BackgroundService
{
    // How often this background job wakes up to check the database
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    // How long a user has to pay before their cart expires
    private readonly TimeSpan _expirationWindow = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Ticket Cleanup Worker is starting.");

        // We use a PeriodicTimer so it wakes up exactly every 1 minute
        using var timer = new PeriodicTimer(_checkInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // Create ONE scope per minute to share the DbContext across both jobs
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Run Job 1: Free up unpaid seats
                await CleanupAbandonedCartsAsync(dbContext, stoppingToken);

                // Run Job 2: Mark past movies as expired for no-shows
                await ExpirePastShowtimeTicketsAsync(dbContext, stoppingToken);

                // Save all changes from both jobs to SQL Server atomically
                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while running the cleanup jobs.");
            }
        }
    }

    // ==========================================
    // JOB 1: Cart Abandonment (Releasing Seats)
    // ==========================================
    private async Task CleanupAbandonedCartsAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var expirationThreshold = DateTime.UtcNow.Subtract(_expirationWindow);

        // Find all PENDING tickets older than 10 minutes
        var expiredTickets = await dbContext.Tickets
            .Include(t => t.Showtime)
            .ThenInclude(s => s!.Auditorium)
            .Where(t => t.Status == TicketStatus.Pending && t.CreatedAtUtc < expirationThreshold)
            .ToListAsync(cancellationToken);

        if (expiredTickets.Count == 0) return;

        logger.LogInformation("Found {Count} abandoned carts. Releasing seats...", expiredTickets.Count);

        foreach (var ticket in expiredTickets)
        {
            var engine = new SeatMapEngine(
                ticket.Showtime!.SeatAvailabilityState,
                ticket.Showtime.Auditorium!.MaxRows,
                ticket.Showtime.Auditorium.MaxColumns);

            // Turn the seats from Reserved (1) back to Available (0)
            foreach (var seat in ticket.ReservedSeats)
            {
                engine.ReleaseSeat(seat.Row, seat.Col);
            }

            // Force EF Core to detect the byte array mutation
            dbContext.Entry(ticket.Showtime).Property(s => s.SeatAvailabilityState).IsModified = true;

            // Mark the ticket as mathematically dead
            ticket.Status = TicketStatus.Expired;
            cache.Remove($"SeatMap_{ticket.ShowtimeId}");

            logger.LogInformation("Abandoned Booking Reference: {Ref}. Seats released.", ticket.BookingReference);
        }
    }

    // ==========================================
    // JOB 2: No-Shows (Movie is Over)
    // ==========================================
    private async Task ExpirePastShowtimeTicketsAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Pre-filter: Grab Confirmed tickets where the movie has ALREADY started 
        // (This saves us from loading future tickets into memory)
        var candidateTickets = await dbContext.Tickets
            .Include(t => t.Showtime)
            .ThenInclude(s => s!.Movie)
            .Where(t => t.Status == TicketStatus.Confirmed && t.Showtime!.StartTimeUtc < now)
            .ToListAsync(cancellationToken);

        // Locally filter to find tickets where StartTime + Runtime has completely passed
        var ticketsToExpire = candidateTickets
            .Where(t => t.Showtime!.StartTimeUtc.AddMinutes(t.Showtime.Movie!.DurationMinutes) < now)
            .ToList();

        if (ticketsToExpire.Count == 0) return;

        logger.LogInformation("Found {Count} unused tickets for past showtimes. Marking as Expired...", ticketsToExpire.Count);

        foreach (var ticket in ticketsToExpire)
        {
            // We DO NOT release the seats here, because the movie is over anyway.
            // We just update the status so the user's digital wallet updates accurately.
            ticket.Status = TicketStatus.Expired;

            logger.LogInformation("Ticket {Ref} marked as Expired (No-Show).", ticket.BookingReference);
        }
    }
}