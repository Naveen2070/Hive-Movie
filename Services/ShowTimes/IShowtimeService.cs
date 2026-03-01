using Hive_Movie.DTOs;
namespace Hive_Movie.Services.ShowTimes;

public interface IShowtimeService
{
    Task<ShowtimeSeatMapResponse> GetSeatMapAsync(Guid showtimeId);
    Task<IEnumerable<ShowtimeResponse>> GetShowtimesByMovieIdAsync(Guid movieId);
    Task<ShowtimeResponse> CreateShowtimeAsync(CreateShowtimeRequest request, string currentUser, bool isAdmin);
    Task UpdateShowtimeAsync(Guid id, UpdateShowtimeRequest request, string currentUser, bool isAdmin);
    Task DeleteShowtimeAsync(Guid id, string currentUser, bool isAdmin);
}