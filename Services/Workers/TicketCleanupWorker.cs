using Hive_Movie.Data;
using Hive_Movie.Engine;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
namespace Hive_Movie.Services.Workers;

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
                await CleanupExpiredTicketsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while cleaning up expired tickets.");
            }
        }
    }

    private async Task CleanupExpiredTicketsAsync(CancellationToken cancellationToken)
    {
        // THE SCOPE TRICK: We must manually create a scope to get a fresh DbContext!
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var expirationThreshold = DateTime.UtcNow.Subtract(_expirationWindow);

        // 1. Find all PENDING tickets older than 10 minutes
        var expiredTickets = await dbContext.Tickets
            .Include(t => t.Showtime)
            .ThenInclude(s => s!.Auditorium)
            .Where(t => t.Status == TicketStatus.Pending && t.CreatedAtUtc < expirationThreshold)
            .ToListAsync(cancellationToken);

        if (expiredTickets.Count == 0)
        {
            return; // Nothing to clean up this minute
        }

        logger.LogInformation("Found {Count} expired tickets. Releasing seats...", expiredTickets.Count);

        // 2. Mathematically free the seats for each expired ticket
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
            logger.LogInformation("Expired Booking Reference: {Ref}. Seats released.", ticket.BookingReference);
        }

        // 3. Save all the released seats back to SQL Server atomically
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}