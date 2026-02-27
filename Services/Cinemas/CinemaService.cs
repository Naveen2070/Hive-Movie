using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Infrastructure.Messaging;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace Hive_Movie.Services.Cinemas;

public class CinemaService(ApplicationDbContext dbContext) : ICinemaService
{
    private readonly static JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IEnumerable<CinemaResponse>> GetAllCinemasAsync()
    {
        var cinemas = await dbContext.Cinemas.ToListAsync();
        return cinemas.Select(CinemaResponse.MapToResponse);
    }

    public async Task<CinemaResponse> GetCinemaByIdAsync(Guid id)
    {
        var cinema = await dbContext.Cinemas.FindAsync(id);
        return cinema == null
            ? throw new KeyNotFoundException($"Cinema with ID {id} not found.")
            : CinemaResponse.MapToResponse(cinema);
    }

    public async Task<CinemaResponse> CreateCinemaAsync(CreateCinemaRequest request, string organizerId)
    {
        var cinema = new Cinema
        {
            Name = request.Name,
            Location = request.Location,
            OrganizerId = organizerId,
            ContactEmail = request.ContactEmail,
            ApprovalStatus = CinemaApprovalStatus.Pending
        };

        dbContext.Cinemas.Add(cinema);

        var emailEvent = new EmailNotificationEvent(
            cinema.ContactEmail,
            "Cinema Registration Received - The Hive",
            "CINEMA_PENDING_APPROVAL",
            new Dictionary<string, string>
            {
                {
                    "cinemaName", cinema.Name
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

        // Atomically save the Cinema and the Outbox Message
        await dbContext.SaveChangesAsync();

        return CinemaResponse.MapToResponse(cinema);
    }

    public async Task UpdateCinemaAsync(Guid id, UpdateCinemaRequest request, string currentUser, bool isAdmin)
    {
        var cinema = await dbContext.Cinemas.FindAsync(id) ?? throw new KeyNotFoundException($"Cinema with ID {id} not found.");

        if (!isAdmin && cinema.OrganizerId != currentUser)
            throw new UnauthorizedAccessException("You are not authorized to update this cinema.");

        cinema.Name = request.Name;
        cinema.Location = request.Location;
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteCinemaAsync(Guid id, string currentUser, bool isAdmin)
    {
        var cinema = await dbContext.Cinemas.FindAsync(id) ?? throw new KeyNotFoundException($"Cinema with ID {id} not found.");

        if (!isAdmin && cinema.OrganizerId != currentUser)
            throw new UnauthorizedAccessException("You are not authorized to delete this cinema.");

        dbContext.Cinemas.Remove(cinema);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateCinemaStatusAsync(Guid id, CinemaApprovalStatus status)
    {
        var cinema = await dbContext.Cinemas.FindAsync(id) ?? throw new KeyNotFoundException($"Cinema with ID {id} not found.");
        cinema.ApprovalStatus = status;

        var emailEvent = new EmailNotificationEvent(
            cinema.ContactEmail,
            $"Cinema {status} - The Hive",
            "CINEMA_STATUS_UPDATE",
            new Dictionary<string, string>
            {
                {
                    "cinemaName", cinema.Name
                },
                {
                    "status", status.ToString()
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

        await dbContext.SaveChangesAsync();
    }
}