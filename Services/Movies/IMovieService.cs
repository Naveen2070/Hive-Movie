using Hive_Movie.DTOs;

namespace Hive_Movie.Services.Movies;

public interface IMovieService
{
    Task<IEnumerable<MovieResponse>> GetAllMoviesAsync();
    Task<MovieResponse> GetMovieByIdAsync(Guid id);
    Task<MovieResponse> CreateMovieAsync(CreateMovieRequest request);
    Task UpdateMovieAsync(Guid id, UpdateMovieRequest request);
    Task DeleteMovieAsync(Guid id);
}
