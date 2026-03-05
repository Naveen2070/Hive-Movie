using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Infrastructure.Clients;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;
namespace Hive_Movie.Services.Dashboard;

public class DashboardService(
    ApplicationDbContext dbContext,
    IIdentityClient identityClient,
    ILogger<DashboardService> logger) : IDashboardService
{
    public async Task<DashboardStatsResponse> GetOrganizerStatsAsync(string organizerId)
    {
        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);
        var fourteenDaysAgo = now.AddDays(-14);
        var thirtyDaysAgo = now.AddDays(-30);
        var sixtyDaysAgo = now.AddDays(-60);

        // 1. Base Queries filtered by Organizer
        var organizerCinemas = dbContext.Cinemas.Where(c => c.OrganizerId == organizerId).Select(c => c.Id);

        var showtimesQuery = dbContext.Showtimes
            .Include(s => s.Auditorium)
            .Where(s => !s.IsDeleted && organizerCinemas.Contains(s.Auditorium!.CinemaId));

        var ticketsQuery = dbContext.Tickets
            .Include(t => t.Showtime).ThenInclude(s => s!.Movie)
            .Include(t => t.Showtime).ThenInclude(s => s!.Auditorium)
            .Where(t => organizerCinemas.Contains(t.Showtime!.Auditorium!.CinemaId));

        // 2. Event/Showtime Stats
        var totalEvents = await showtimesQuery.CountAsync();
        var activeEvents = await showtimesQuery.CountAsync(s => s.StartTimeUtc >= now);

        // 3. Ticket Stats
        var confirmedTickets = await ticketsQuery
            .Where(t => t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used)
            .ToListAsync();

        var pendingPaymentTickets = await ticketsQuery
            .Where(t => t.Status == TicketStatus.Pending)
            .SumAsync(t => t.ReservedSeats.Count);

        var totalTicketsSold = confirmedTickets.Sum(t => t.ReservedSeats.Count);
        var ticketsSoldLastWeek = confirmedTickets.Where(t => t.CreatedAtUtc >= sevenDaysAgo).Sum(t => t.ReservedSeats.Count);

        // 4. Revenue & Growth Math
        var totalRevenue = confirmedTickets.Sum(t => t.TotalAmount);

        var revLast7Days = confirmedTickets.Where(t => t.CreatedAtUtc >= sevenDaysAgo).Sum(t => t.TotalAmount);
        var revPrevious7Days = confirmedTickets.Where(t => t.CreatedAtUtc >= fourteenDaysAgo && t.CreatedAtUtc < sevenDaysAgo).Sum(t => t.TotalAmount);
        var growthLastWeek = CalculateGrowth(revLast7Days, revPrevious7Days);

        var revLast30Days = confirmedTickets.Where(t => t.CreatedAtUtc >= thirtyDaysAgo).Sum(t => t.TotalAmount);
        var revPrevious30Days = confirmedTickets.Where(t => t.CreatedAtUtc >= sixtyDaysAgo && t.CreatedAtUtc < thirtyDaysAgo).Sum(t => t.TotalAmount);
        var growthLastMonth = CalculateGrowth(revLast30Days, revPrevious30Days);

        // 5. Generate 30-Day Revenue Trend
        var revenueTrend = new List<RevenueTrendItem>();
        for (var i = 29; i >= 0; i--)
        {
            var date = now.AddDays(-i).Date;
            var dailyRevenue = confirmedTickets
                .Where(t => t.CreatedAtUtc.Date == date)
                .Sum(t => t.TotalAmount);

            revenueTrend.Add(new RevenueTrendItem(date.ToString("yyyy-MM-dd"), dailyRevenue));
        }

        // 6. Recent Sales (Top 5)
        var recentTickets = confirmedTickets
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(5)
            .ToList();

        // 6a. Batch fetch user names from Identity Service
        var userNamesLookup = new Dictionary<string, string>();
        var userIdsToFetch = recentTickets
            .Select(t => t.UserId)
            .Distinct()
            .Where(id => long.TryParse(id, out _))
            .Select(long.Parse)
            .ToList();

        if (userIdsToFetch.Count != 0)
        {
            try
            {
                var users = await identityClient.GetUsersByIdsAsync(userIdsToFetch);
                foreach (var user in users)
                {
                    userNamesLookup[user.Id.ToString()] = !string.IsNullOrWhiteSpace(user.FullName)
                        ? user.FullName
                        : user.Email;
                }
            }
            catch (Exception ex)
            {
                // Graceful degradation: If Identity Service is down, we just show "Guest"
                logger.LogWarning(ex, "Failed to batch fetch user details from Identity Service for the Dashboard.");
            }
        }

        // 6b. Map tickets to RecentSale DTOs, computing dynamic Tier names
        var recentSalesList = new List<RecentSale>();
        foreach (var t in recentTickets)
        {
            var customerName = userNamesLookup.TryGetValue(t.UserId, out var name)
                ? name
                : "Guest";

            // Map the exact seating tiers
            var layout = t.Showtime!.Auditorium!.LayoutConfiguration;
            var seatTierLookup = layout.Tiers
                .SelectMany(tier => tier.Seats.Select(seat =>
                    new KeyValuePair<(int Row, int Col), string>((seat.Row, seat.Col), tier.TierName)))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var uniqueTiers = t.ReservedSeats
                .Select(seat => seatTierLookup.TryGetValue((seat.Row, seat.Col), out var tier)
                    ? tier
                    : "Standard")
                .Distinct()
                .ToList();

            // e.g., "VIP, Standard"
            var combinedTierName = string.Join(", ", uniqueTiers);

            recentSalesList.Add(new RecentSale(
                t.Id.ToString(),
                t.Showtime!.Movie!.Title,
                customerName,
                string.IsNullOrWhiteSpace(combinedTierName)
                    ? "Standard"
                    : combinedTierName,
                t.ReservedSeats.Count,
                t.TotalAmount,
                t.CreatedAtUtc.ToString("o")
            ));
        }

        return new DashboardStatsResponse(
            totalRevenue,
            growthLastWeek,
            growthLastMonth,
            totalTicketsSold,
            pendingPaymentTickets,
            ticketsSoldLastWeek,
            activeEvents,
            totalEvents,
            revenueTrend,
            recentSalesList
        );
    }

    private static double CalculateGrowth(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            return current > 0
                ? 100.0
                : 0.0;
        }

        return (double)((current - previous) / previous * 100);
    }
}