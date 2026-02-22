using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;

namespace Hive_Movie.Services.Cinemas;

public class CinemaService(ApplicationDbContext dbContext) : ICinemaService
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<IEnumerable<CinemaResponse>> GetAllCinemasAsync()
    {
        var cinemas = await _dbContext.Cinemas.ToListAsync();
        return cinemas.Select(c => new CinemaResponse(c.Id, c.Name, c.Location));
    }

    public async Task<CinemaResponse> GetCinemaByIdAsync(Guid id)
    {
        var cinema = await _dbContext.Cinemas.FindAsync(id);
        return cinema == null
            ? throw new KeyNotFoundException($"Cinema with ID {id} not found.")
            : new CinemaResponse(cinema.Id, cinema.Name, cinema.Location);
    }

    public async Task<CinemaResponse> CreateCinemaAsync(CreateCinemaRequest request)
    {
        var cinema = new Cinema
        {
            Name = request.Name,
            Location = request.Location
        };

        _dbContext.Cinemas.Add(cinema);
        await _dbContext.SaveChangesAsync();

        return new CinemaResponse(cinema.Id, cinema.Name, cinema.Location);
    }

    public async Task UpdateCinemaAsync(Guid id, UpdateCinemaRequest request)
    {
        var cinema = await _dbContext.Cinemas.FindAsync(id) 
            ?? throw new KeyNotFoundException($"Cinema with ID {id} not found.");

        cinema.Name = request.Name;
        cinema.Location = request.Location;
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteCinemaAsync(Guid id)
    {
        var cinema = await _dbContext.Cinemas.FindAsync(id) 
            ?? throw new KeyNotFoundException($"Cinema with ID {id} not found.");

        _dbContext.Cinemas.Remove(cinema);
        await _dbContext.SaveChangesAsync();
    }
}