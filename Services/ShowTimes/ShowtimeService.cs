using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Engine;
using Microsoft.EntityFrameworkCore;

namespace Hive_Movie.Services.ShowTimes;

public class ShowtimeService(ApplicationDbContext dbContext) : IShowtimeService
{
    public async Task<ShowtimeSeatMapResponse> GetSeatMapAsync(Guid showtimeId)
    {
        var showtime = await dbContext.Showtimes
            .Include(s => s.Movie)
            .Include(s => s.Auditorium)
            .ThenInclude(a => a!.Cinema)
            .FirstOrDefaultAsync(s => s.Id == showtimeId);

        if (showtime?.Auditorium == null || showtime.Movie == null)
            throw new KeyNotFoundException("Showtime not found.");

        var engine = new SeatMapEngine(
            showtime.SeatAvailabilityState,
            showtime.Auditorium.MaxRows,
            showtime.Auditorium.MaxColumns);

        var seatMap = new List<SeatStatusDto>();

        for (var r = 0; r < showtime.Auditorium.MaxRows; r++)
        {
            for (var c = 0; c < showtime.Auditorium.MaxColumns; c++)
            {
                seatMap.Add(new SeatStatusDto(r, c, engine.GetStatus(r, c).ToString()));
            }
        }

        return new ShowtimeSeatMapResponse(
            showtime.Movie.Title,
            showtime.Auditorium.Cinema!.Name,
            showtime.Auditorium.Name,
            showtime.Auditorium.MaxRows,
            showtime.Auditorium.MaxColumns,
            seatMap
        );
    }

    public async Task ReserveSeatsAsync(Guid showtimeId, ReserveSeatsRequest request)
    {
        if (request.Seats == null || request.Seats.Count == 0)
            throw new ArgumentException("You must select at least one seat.");

        var showtime = await dbContext.Showtimes
            .Include(s => s.Auditorium)
            .FirstOrDefaultAsync(s => s.Id == showtimeId);

        if (showtime?.Auditorium == null)
            throw new KeyNotFoundException("Showtime not found.");

        var engine = new SeatMapEngine(
            showtime.SeatAvailabilityState,
            showtime.Auditorium.MaxRows,
            showtime.Auditorium.MaxColumns);

        var seatsToBook = request.Seats.Select(s => (s.Row, s.Col)).ToList();

        if (!engine.TryReserveSeats(seatsToBook))
            throw new InvalidOperationException("One or more selected seats are no longer available.");

        // Force EF Core to detect the byte array mutation
        dbContext.Entry(showtime).Property(s => s.SeatAvailabilityState).IsModified = true;

        await dbContext.SaveChangesAsync();
    }
}
