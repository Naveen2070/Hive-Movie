using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Engine;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;
namespace Hive_Movie.Services.Tickets;

public class TicketService(ApplicationDbContext dbContext) : ITicketService
{
    public async Task<TicketCheckoutResponse> ReserveTicketsAsync(ReserveTicketRequest request, string currentUserId)
    {
        // 1. Fetch the Showtime and its Layout
        var showtime = await dbContext.Showtimes
                .Include(s => s.Auditorium)
                .FirstOrDefaultAsync(s => s.Id == request.ShowtimeId)
            ?? throw new KeyNotFoundException("Showtime not found.");

        var layout = showtime.Auditorium!.LayoutConfiguration;

        // 2. Run the High-Performance Seat Engine to lock the byte array
        var engine = new SeatMapEngine(
            showtime.SeatAvailabilityState,
            showtime.Auditorium.MaxRows,
            showtime.Auditorium.MaxColumns);

        var requestedCoordinates = request.Seats.Select(s => (s.Row, s.Col)).ToList();

        if (!engine.TryReserveSeats(requestedCoordinates))
            throw new InvalidOperationException("One or more selected seats are no longer available.");

        dbContext.Entry(showtime).Property(s => s.SeatAvailabilityState).IsModified = true;

        // 3. OPTIMIZED PRICING CALCULATOR (O(1) Lookups)
        // Flatten the Tiers JSON into a fast lookup Dictionary: Key = (Row, Col), Value = Surcharge
        var tierSurchargeLookup = layout.Tiers
            .SelectMany(tier => tier.Seats.Select(seat =>
                new KeyValuePair<(int Row, int Col), decimal>((seat.Row, seat.Col), tier.PriceSurcharge)))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        decimal totalAmount = 0;
        foreach (var seat in request.Seats)
        {
            var seatPrice = showtime.BasePrice;

            // O(1) dictionary lookup instead of a nested LINQ loop!
            if (tierSurchargeLookup.TryGetValue((seat.Row, seat.Col), out var surcharge))
            {
                seatPrice += surcharge;
            }

            totalAmount += seatPrice;
        }

        // 4. Generate the Ticket
        var ticket = new Ticket
        {
            UserId = currentUserId,
            ShowtimeId = request.ShowtimeId,
            BookingReference = GenerateBookingReference(),
            TotalAmount = totalAmount,
            Status = TicketStatus.Pending,
            ReservedSeats = request.Seats.Select(s => new SeatCoordinate
            {
                Row = s.Row, Col = s.Col
            }).ToList()
        };

        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        return new TicketCheckoutResponse(
            ticket.Id,
            ticket.BookingReference,
            ticket.TotalAmount,
            ticket.Status.ToString(),
            ticket.CreatedAtUtc);
    }

    public async Task<IEnumerable<MyTicketResponse>> GetMyTicketsAsync(string currentUserId)
    {
        var tickets = await dbContext.Tickets
            .AsNoTracking()
            .Include(t => t.Showtime)
            .ThenInclude(s => s!.Movie)
            .Include(t => t.Showtime)
            .ThenInclude(s => s!.Auditorium)
            .ThenInclude(a => a!.Cinema)
            .Where(t => t.UserId == currentUserId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync();

        return tickets.Select(t => new MyTicketResponse(
            t.Id,
            t.BookingReference,
            t.Showtime!.Movie!.Title,
            t.Showtime.Auditorium!.Cinema!.Name,
            t.Showtime.Auditorium.Name,
            t.Showtime.StartTimeUtc,
            t.ReservedSeats.Select(s => new SeatCoordinateDto(s.Row, s.Col)).ToList(),
            t.TotalAmount,
            t.Status.ToString(),
            t.CreatedAtUtc
        ));
    }

    // Helper: Generates a unique, human-readable reference using a UUID
    private static string GenerateBookingReference()
    {
        // Creates a UUID (e.g. 5b3a4f...), removes hyphens, takes the first 8 chars, and capitalizes it.
        var shortUuid = Guid.NewGuid().ToString("N")[..8].ToUpper();
        return $"HIVE-{shortUuid}";
    }
}