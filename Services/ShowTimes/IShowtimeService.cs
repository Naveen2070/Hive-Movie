using Hive_Movie.DTOs;
namespace Hive_Movie.Services.ShowTimes;

public interface IShowtimeService
{
    Task<ShowtimeSeatMapResponse> GetSeatMapAsync(Guid showtimeId);

    Task<PagedResponse<ShowtimeResponse>> GetShowtimesByMovieIdAsync(
        Guid movieId,
        int page = 0,
        int size = 20,
        DateTime? fromDate = null,
        DateTime? toDate = null);

    Task<ShowtimeResponse> CreateShowtimeAsync(CreateShowtimeRequest request, string currentUser, bool isAdmin);
    Task UpdateShowtimeAsync(Guid id, UpdateShowtimeRequest request, string currentUser, bool isAdmin);
    Task DeleteShowtimeAsync(Guid id, string currentUser, bool isAdmin);
}