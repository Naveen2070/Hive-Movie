using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;

namespace Hive_Movie.Services.Movies;

public class MovieService(ApplicationDbContext dbContext) : IMovieService
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<IEnumerable<MovieResponse>> GetAllMoviesAsync()
    {
        var movies = await _dbContext.Movies
            .OrderByDescending(m => m.ReleaseDate)
            .ToListAsync();

        return movies.Select(m => new MovieResponse(
            m.Id, m.Title, m.Description, m.DurationMinutes, m.ReleaseDate, m.PosterUrl));
    }

    public async Task<MovieResponse> GetMovieByIdAsync(Guid id)
    {
        var movie = await _dbContext.Movies.FindAsync(id);
        return movie == null
            ? throw new KeyNotFoundException($"Movie with ID {id} not found.")
            : new MovieResponse(
            movie.Id, movie.Title, movie.Description, movie.DurationMinutes, movie.ReleaseDate, movie.PosterUrl);
    }

    public async Task<MovieResponse> CreateMovieAsync(CreateMovieRequest request)
    {
        var movie = new Movie
        {
            Title = request.Title,
            Description = request.Description,
            DurationMinutes = request.DurationMinutes,
            ReleaseDate = request.ReleaseDate,
            PosterUrl = request.PosterUrl
        };

        _dbContext.Movies.Add(movie);
        await _dbContext.SaveChangesAsync();

        return new MovieResponse(
            movie.Id, movie.Title, movie.Description, movie.DurationMinutes, movie.ReleaseDate, movie.PosterUrl);
    }

    public async Task UpdateMovieAsync(Guid id, UpdateMovieRequest request)
    {
        var movie = await _dbContext.Movies.FindAsync(id)
            ?? throw new KeyNotFoundException($"Movie with ID {id} not found.");

        movie.Title = request.Title;
        movie.Description = request.Description;
        movie.DurationMinutes = request.DurationMinutes;
        movie.ReleaseDate = request.ReleaseDate;
        movie.PosterUrl = request.PosterUrl;

        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteMovieAsync(Guid id)
    {
        var movie = await _dbContext.Movies.FindAsync(id)
            ?? throw new KeyNotFoundException($"Movie with ID {id} not found.");

        _dbContext.Movies.Remove(movie);
        await _dbContext.SaveChangesAsync();
    }
}