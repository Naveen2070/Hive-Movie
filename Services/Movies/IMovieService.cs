using Hive_Movie.DTOs;
namespace Hive_Movie.Services.Movies;

public interface IMovieService
{
    Task<PagedResponse<MovieResponse>> GetAllMoviesAsync(int page = 0, int size = 10, string? search = null);
    Task<MovieResponse> GetMovieByIdAsync(Guid id);
    Task<MovieResponse> CreateMovieAsync(CreateMovieRequest request);
    Task UpdateMovieAsync(Guid id, UpdateMovieRequest request);
    Task DeleteMovieAsync(Guid id);
}