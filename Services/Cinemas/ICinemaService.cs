using Hive_Movie.DTOs;
using Hive_Movie.Models;

namespace Hive_Movie.Services.Cinemas;

public interface ICinemaService
{
    Task<IEnumerable<CinemaResponse>> GetAllCinemasAsync();
    Task<CinemaResponse> GetCinemaByIdAsync(Guid id);
    Task<CinemaResponse> CreateCinemaAsync(CreateCinemaRequest request, string organizerId);
    Task UpdateCinemaAsync(Guid id, UpdateCinemaRequest request);
    Task DeleteCinemaAsync(Guid id);
    Task UpdateCinemaStatusAsync(Guid id, CinemaApprovalStatus status);
}