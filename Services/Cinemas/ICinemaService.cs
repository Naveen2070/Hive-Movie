using Hive_Movie.DTOs;

namespace Hive_Movie.Services.Cinemas;

public interface ICinemaService
{
    Task<IEnumerable<CinemaResponse>> GetAllCinemasAsync();
    Task<CinemaResponse> GetCinemaByIdAsync(Guid id);
    Task<CinemaResponse> CreateCinemaAsync(CreateCinemaRequest request);
    Task UpdateCinemaAsync(Guid id, UpdateCinemaRequest request);
    Task DeleteCinemaAsync(Guid id);
}