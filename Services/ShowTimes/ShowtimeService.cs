using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Engine;
using Hive_Movie.Models;
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

    public async Task<ShowtimeResponse> CreateShowtimeAsync(CreateShowtimeRequest request, string currentUser, bool isAdmin)
    {
        // 1. Fetch Auditorium & Verify Ownership and Approval Status
        var auditorium = await GetAuditoriumWithSecurityCheckAsync(request.AuditoriumId, currentUser, isAdmin, true);

        // 2. Verify Movie exists
        if (!await dbContext.Movies.AnyAsync(m => m.Id == request.MovieId))
            throw new KeyNotFoundException("The specified Movie ID does not exist.");

        // 3. Initialize the high-performance byte array based on room size
        var totalSeats = auditorium.MaxRows * auditorium.MaxColumns;

        var showtime = new Showtime
        {
            MovieId = request.MovieId,
            AuditoriumId = request.AuditoriumId,
            StartTimeUtc = request.StartTimeUtc,
            BasePrice = request.BasePrice,
            SeatAvailabilityState = new byte[totalSeats]
        };

        dbContext.Showtimes.Add(showtime);
        await dbContext.SaveChangesAsync();

        return new ShowtimeResponse(showtime.Id, showtime.MovieId, showtime.AuditoriumId, showtime.StartTimeUtc, showtime.BasePrice);
    }

    public async Task UpdateShowtimeAsync(Guid id, UpdateShowtimeRequest request, string currentUser, bool isAdmin)
    {
        var showtime = await GetShowtimeWithSecurityCheckAsync(id, currentUser, isAdmin);

        showtime.StartTimeUtc = request.StartTimeUtc;
        showtime.BasePrice = request.BasePrice;

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteShowtimeAsync(Guid id, string currentUser, bool isAdmin)
    {
        var showtime = await GetShowtimeWithSecurityCheckAsync(id, currentUser, isAdmin);

        dbContext.Showtimes.Remove(showtime);
        await dbContext.SaveChangesAsync();
    }

    // --- SECURITY HELPER METHODS ---

    private async Task<Auditorium> GetAuditoriumWithSecurityCheckAsync(
        Guid auditoriumId,
        string currentUser,
        bool isAdmin,
        bool requiresApprovedCinema = false)
    {
        var auditorium = await dbContext.Auditoriums
                .Include(a => a.Cinema)
                .FirstOrDefaultAsync(a => a.Id == auditoriumId)
            ?? throw new KeyNotFoundException($"Auditorium with ID {auditoriumId} not found.");

        if (!isAdmin && auditorium.Cinema!.OrganizerId != currentUser)
            throw new UnauthorizedAccessException("You do not own the cinema this auditorium belongs to.");

        // STRICT RULE: Cannot schedule movies in a pending cinema!
        if (requiresApprovedCinema && auditorium.Cinema!.ApprovalStatus != CinemaApprovalStatus.Approved)
            throw new InvalidOperationException("Cannot add showtimes to a cinema that has not been approved by an Admin.");

        return auditorium;
    }

    private async Task<Showtime> GetShowtimeWithSecurityCheckAsync(Guid showtimeId, string currentUser, bool isAdmin)
    {
        var showtime = await dbContext.Showtimes
                .Include(s => s.Auditorium)
                .ThenInclude(a => a!.Cinema)
                .FirstOrDefaultAsync(s => s.Id == showtimeId)
            ?? throw new KeyNotFoundException($"Showtime with ID {showtimeId} not found.");

        if (!isAdmin && showtime.Auditorium!.Cinema!.OrganizerId != currentUser)
            throw new UnauthorizedAccessException("You do not own the cinema running this showtime.");

        return showtime;
    }
}