using Hive_Movie.DTOs;

namespace Hive_Movie.Services.Auditoriums;

public interface IAuditoriumService
{
    Task<IEnumerable<AuditoriumResponse>> GetAllAuditoriumsAsync();
    Task<IEnumerable<AuditoriumResponse>> GetAuditoriumsByCinemaIdAsync(Guid cinemaId);
    Task<AuditoriumResponse> GetAuditoriumByIdAsync(Guid id);
    Task<AuditoriumResponse> CreateAuditoriumAsync(CreateAuditoriumRequest request);
    Task UpdateAuditoriumAsync(Guid id, UpdateAuditoriumRequest request);
    Task DeleteAuditoriumAsync(Guid id);
}