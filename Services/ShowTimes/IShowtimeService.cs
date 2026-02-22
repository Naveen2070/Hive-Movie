using Hive_Movie.DTOs;

namespace Hive_Movie.Services.ShowTimes;

public interface IShowtimeService
{
    Task<ShowtimeSeatMapResponse> GetSeatMapAsync(Guid showtimeId);
    Task ReserveSeatsAsync(Guid showtimeId, ReserveSeatsRequest request);
}
