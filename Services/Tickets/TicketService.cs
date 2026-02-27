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
            .Include(t => t.Showtime).ThenInclude(s => s!.Movie)
            .Include(t => t.Showtime).ThenInclude(s => s!.Auditorium).ThenInclude(a => a!.Cinema)
            .Where(t => t.UserId == currentUserId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync();

        return tickets.Select(t => new MyTicketResponse(
            t.Id, t.BookingReference, t.Showtime!.Movie!.Title, t.Showtime.Auditorium!.Cinema!.Name,
            t.Showtime.Auditorium.Name, t.Showtime.StartTimeUtc,
            t.ReservedSeats.Select(s => new SeatCoordinateDto(s.Row, s.Col)).ToList(),
            t.TotalAmount, t.Status.ToString(), t.CreatedAtUtc
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

    private static string GenerateBookingReference()
    {
        var shortUuid = Guid.NewGuid().ToString("N")[..8].ToUpper();
        return $"HIVE-{shortUuid}";
    }
}