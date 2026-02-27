using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Engine;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
namespace Hive_Movie.Services.ShowTimes;

public class ShowtimeService(
    ApplicationDbContext dbContext,
    IMemoryCache cache) : IShowtimeService
{
    public async Task<ShowtimeSeatMapResponse> GetSeatMapAsync(Guid showtimeId)
    {
        var cacheKey = $"SeatMap_{showtimeId}";

        // 1. Check RAM first! (O(1) nanosecond lookup)
        if (cache.TryGetValue(cacheKey, out ShowtimeSeatMapResponse? cachedMap))
        {
            return cachedMap!;
        }

        // 2. Not in RAM? Fetch from SQL Server
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

        var response = new ShowtimeSeatMapResponse(
            showtime.Movie.Title,
            showtime.Auditorium.Cinema!.Name,
            showtime.Auditorium.Name,
            showtime.Auditorium.MaxRows,
            showtime.Auditorium.MaxColumns,
            seatMap
        );

        // 3. Save to RAM for 60 seconds
        cache.Set(cacheKey, response, TimeSpan.FromSeconds(60));

        return response;
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