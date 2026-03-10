using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Engine;
using Hive_Movie.Infrastructure.Clients;
using Hive_Movie.Infrastructure.Messaging;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
namespace Hive_Movie.Services.Tickets;

public class TicketService(
    ApplicationDbContext dbContext,
    IMemoryCache cache,
    IIdentityClient identityClient,
    ILogger<TicketService> logger) : ITicketService
{
    private readonly static JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<TicketCheckoutResponse> ReserveTicketsAsync(ReserveTicketRequest request, string currentUserId)
    {
        var showtime = await dbContext.Showtimes
                .Include(s => s.Auditorium)
                .FirstOrDefaultAsync(s => s.Id == request.ShowtimeId)
            ?? throw new KeyNotFoundException("Showtime not found.");

        var layout = showtime.Auditorium!.LayoutConfiguration;

        var engine = new SeatMapEngine(showtime.SeatAvailabilityState, showtime.Auditorium.MaxRows, showtime.Auditorium.MaxColumns);
        var requestedCoordinates = request.Seats.Select(s => (s.Row, s.Col)).ToList();

        if (!engine.TryReserveSeats(requestedCoordinates))
            throw new InvalidOperationException("One or more selected seats are no longer available.");

        dbContext.Entry(showtime).Property(s => s.SeatAvailabilityState).IsModified = true;

        var tierSurchargeLookup = layout.Tiers
            .SelectMany(tier => tier.Seats.Select(seat => new KeyValuePair<(int Row, int Col), decimal>((seat.Row, seat.Col), tier.PriceSurcharge)))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        decimal totalAmount = 0;
        foreach (var seat in request.Seats)
        {
            var seatPrice = showtime.BasePrice;
            if (tierSurchargeLookup.TryGetValue((seat.Row, seat.Col), out var surcharge)) seatPrice += surcharge;
            totalAmount += seatPrice;
        }

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

        return new TicketCheckoutResponse(ticket.Id, ticket.BookingReference, ticket.TotalAmount, ticket.Status.ToString(), ticket.CreatedAtUtc);
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
            t.Id, // TicketId
            t.Showtime!.MovieId, // MovieId 
            t.Showtime.Auditorium!.CinemaId, // CinemaId 
            t.ShowtimeId, // ShowtimeId 
            t.BookingReference, // BookingReference
            t.Showtime.Movie!.Title, // MovieTitle
            t.Showtime.Auditorium.Cinema!.Name, // CinemaName
            t.Showtime.Auditorium.Name, // AuditoriumName
            t.Showtime.StartTimeUtc, // StartTimeUtc
            t.ReservedSeats.Select(s => new SeatCoordinateDto(s.Row, s.Col)).ToList(), // ReservedSeats
            t.TotalAmount, // TotalAmount
            t.Status.ToString(), // Status
            t.CreatedAtUtc // CreatedAtUtc
        ));
    }

    public async Task ConfirmTicketPaymentAsync(string bookingReference)
    {
        var ticket = await dbContext.Tickets
                .Include(t => t.Showtime).ThenInclude(s => s!.Auditorium).ThenInclude(a => a!.Cinema)
                .Include(t => t.Showtime!.Movie)
                .FirstOrDefaultAsync(t => t.BookingReference == bookingReference)
            ?? throw new KeyNotFoundException($"No ticket found with reference {bookingReference}");

        if (ticket.Status == TicketStatus.Confirmed) return;

        if (ticket.Status != TicketStatus.Pending)
            throw new InvalidOperationException($"Ticket is in {ticket.Status} state and cannot be confirmed.");

        var engine = new SeatMapEngine(ticket.Showtime!.SeatAvailabilityState, ticket.Showtime.Auditorium!.MaxRows,
            ticket.Showtime.Auditorium.MaxColumns);
        foreach (var seat in ticket.ReservedSeats)
        {
            engine.MarkAsSold(seat.Row, seat.Col);
        }

        dbContext.Entry(ticket.Showtime).Property(s => s.SeatAvailabilityState).IsModified = true;

        ticket.Status = TicketStatus.Confirmed;
        ticket.PaidAtUtc = DateTime.UtcNow;

        try
        {
            if (long.TryParse(ticket.UserId, out var parsedUserId))
            {
                var userDto = await identityClient.GetUserByIdAsync(parsedUserId);

                var emailEvent = new EmailNotificationEvent(
                    userDto.Email,
                    "Ticket Confirmed! - The Hive",
                    "TICKET_CONFIRMATION",
                    new Dictionary<string, string>
                    {
                        {
                            "bookingRef", ticket.BookingReference
                        },
                        {
                            "movieTitle", ticket.Showtime.Movie!.Title
                        },
                        {
                            "cinemaName", ticket.Showtime.Auditorium.Cinema!.Name
                        },
                        {
                            "totalAmount", ticket.TotalAmount.ToString("C")
                        }
                    }
                );

                dbContext.OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = "EmailNotification",
                    Payload = JsonSerializer.Serialize(emailEvent, JsonOptions),
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                logger.LogWarning("UserId {UserId} is not a valid long, skipping outbox message.", ticket.UserId);
            }
        }
        catch (Exception ex)
        {
            // We catch IdentityClient errors so the payment confirmation doesn't fail if Identity Service is down
            logger.LogError(ex, "Failed to call IdentityService for Ticket {Ref}. Email will not be sent.", bookingReference);
        }

        await dbContext.SaveChangesAsync();
        cache.Remove($"SeatMap_{ticket.ShowtimeId}");
    }

    public async Task<CheckInResponse> CheckInAsync(string bookingReference)
    {
        // 1. Fetch Ticket + Showtime + Auditorium to get the Layout Configuration
        var ticket = await dbContext.Tickets
            .Include(t => t.Showtime)
            .ThenInclude(s => s!.Auditorium)
            .Include(t => t.Showtime)
            .ThenInclude(s => s!.Movie)
            .FirstOrDefaultAsync(t => t.BookingReference == bookingReference);

        if (ticket == null)
        {
            return new CheckInResponse("NOT_FOUND", "Unknown", "Unknown");
        }

        // 2. Fetch Attendee Details gracefully
        var attendeeName = "Guest";
        try
        {
            if (long.TryParse(ticket.UserId, out var parsedUserId))
            {
                var userDto = await identityClient.GetUserByIdAsync(parsedUserId);
                attendeeName = !string.IsNullOrWhiteSpace(userDto.FullName)
                    ? userDto.FullName
                    : userDto.Email;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch user details for check-in. UserId: {UserId}", ticket.UserId);
        }

        // 3. Resolve the Actual Ticket Tiers
        var layout = ticket.Showtime!.Auditorium!.LayoutConfiguration;

        // Create a quick lookup dictionary for (Row, Col) -> Tier Name
        var seatTierLookup = layout.Tiers.SelectMany(tier => tier.Seats.Select(seat =>
                new KeyValuePair<(int Row, int Col), string>((seat.Row, seat.Col), tier.TierName)))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        // Count how many seats belong to each tier
        var tierCounts = new Dictionary<string, int>();
        foreach (var seat in ticket.ReservedSeats)
        {
            var tierName = seatTierLookup.TryGetValue((seat.Row, seat.Col), out var name)
                ? name
                : "Standard";

            if (!tierCounts.TryGetValue(tierName, out var count))
            {
                count = 0;
            }
            tierCounts[tierName] = count + 1;
        }

        // Format into a clean string: "VIP (2 Seats), Standard (1 Seat)"
        var ticketTier = string.Join(", ", tierCounts.Select(kv =>
            $"{kv.Key} ({kv.Value} Seat{(kv.Value > 1 ? "s" : "")})"));

        // 4. Check statuses
        if (ticket.Status == TicketStatus.Used)
        {
            return new CheckInResponse("ALREADY_CHECKED_IN", attendeeName, ticketTier);
        }

        if (ticket.Status != TicketStatus.Confirmed)
        {
            return new CheckInResponse("INVALID_STATUS", attendeeName, ticketTier);
        }

        var movieEndTimeUtc = ticket.Showtime.StartTimeUtc.AddMinutes(ticket.Showtime.Movie!.DurationMinutes);

        if (DateTime.UtcNow > movieEndTimeUtc)
        {
            return new CheckInResponse("EXPIRED", attendeeName, ticketTier);
        }

        // 5. Success - Mark as Used
        ticket.Status = TicketStatus.Used;
        await dbContext.SaveChangesAsync();

        return new CheckInResponse("CHECKED_IN", attendeeName, ticketTier);
    }

    private static string GenerateBookingReference()
    {
        var shortUuid = Guid.NewGuid().ToString("N")[..8].ToUpper();
        return $"HIVE-{shortUuid}";
    }
}