using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;

namespace Hive_Movie.Services.Cinemas;

public class CinemaService(ApplicationDbContext dbContext) : ICinemaService
{
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
        await dbContext.SaveChangesAsync();

        return CinemaResponse.MapToResponse(cinema);
    }

    public async Task UpdateCinemaAsync(Guid id, UpdateCinemaRequest request)
    {
        var cinema = await dbContext.Cinemas.FindAsync(id) 
            ?? throw new KeyNotFoundException($"Cinema with ID {id} not found.");

        cinema.Name = request.Name;
        cinema.Location = request.Location;
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteCinemaAsync(Guid id)
    {
        var cinema = await dbContext.Cinemas.FindAsync(id) 
            ?? throw new KeyNotFoundException($"Cinema with ID {id} not found.");

        dbContext.Cinemas.Remove(cinema);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateCinemaStatusAsync(Guid id, CinemaApprovalStatus status)
    {
        var cinema = await dbContext.Cinemas.FindAsync(id) 
            ?? throw new KeyNotFoundException($"Cinema with ID {id} not found.");

        cinema.ApprovalStatus = status;
        await dbContext.SaveChangesAsync();
    }
}