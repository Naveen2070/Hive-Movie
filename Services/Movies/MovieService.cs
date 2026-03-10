using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;
namespace Hive_Movie.Services.Movies;

public class MovieService(ApplicationDbContext dbContext) : IMovieService
{
    public async Task<PagedResponse<MovieResponse>> GetAllMoviesAsync(int page = 0, int size = 10, string? search = null)
    {
        var query = dbContext.Movies.AsNoTracking().Where(m => !m.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => m.Title.Contains(search) || m.Description.Contains(search));
        }

        var totalElements = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalElements / (double)size);

        var movies = await query
            .OrderByDescending(m => m.ReleaseDate)
            .Skip(page * size)
            .Take(size)
            .ToListAsync();

        var content = movies.Select(m => new MovieResponse(
            m.Id, m.Title, m.Description, m.DurationMinutes, m.ReleaseDate, m.PosterUrl
        ));

        return new PagedResponse<MovieResponse>(
            content, page, size, totalElements, totalPages, page >= totalPages - 1);
    }

    public async Task<MovieResponse> GetMovieByIdAsync(Guid id)
    {
        var movie = await dbContext.Movies.FindAsync(id);
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

        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        return new MovieResponse(
            movie.Id, movie.Title, movie.Description, movie.DurationMinutes, movie.ReleaseDate, movie.PosterUrl);
    }

    public async Task UpdateMovieAsync(Guid id, UpdateMovieRequest request)
    {
        var movie = await dbContext.Movies.FindAsync(id)
            ?? throw new KeyNotFoundException($"Movie with ID {id} not found.");

        movie.Title = request.Title;
        movie.Description = request.Description;
        movie.DurationMinutes = request.DurationMinutes;
        movie.ReleaseDate = request.ReleaseDate;
        movie.PosterUrl = request.PosterUrl;

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteMovieAsync(Guid id)
    {
        var movie = await dbContext.Movies.FindAsync(id)
            ?? throw new KeyNotFoundException($"Movie with ID {id} not found.");

        dbContext.Movies.Remove(movie);
        await dbContext.SaveChangesAsync();
    }
}