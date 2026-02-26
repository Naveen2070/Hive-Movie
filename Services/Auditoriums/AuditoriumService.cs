using FluentValidation;
using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;
namespace Hive_Movie.Services.Auditoriums;

public class AuditoriumService(
    ApplicationDbContext dbContext,
    IValidator<CreateAuditoriumRequest> createValidator,
    IValidator<UpdateAuditoriumRequest> updateValidator) : IAuditoriumService
{
    public async Task<IEnumerable<AuditoriumResponse>> GetAllAuditoriumsAsync()
    {
        var auditoriums = await dbContext.Auditoriums.ToListAsync();
        return auditoriums.Select(MapToResponse);
    }

    public async Task<IEnumerable<AuditoriumResponse>> GetAuditoriumsByCinemaIdAsync(Guid cinemaId)
    {
        var auditoriums = await dbContext.Auditoriums
            .Where(a => a.CinemaId == cinemaId)
            .ToListAsync();

        return auditoriums.Select(MapToResponse);
    }

    public async Task<AuditoriumResponse> GetAuditoriumByIdAsync(Guid id)
    {
        var auditorium = await dbContext.Auditoriums.FindAsync(id);
        return auditorium == null
            ? throw new KeyNotFoundException($"Auditorium with ID {id} not found.")
            : MapToResponse(auditorium);
    }

    public async Task<AuditoriumResponse> CreateAuditoriumAsync(CreateAuditoriumRequest request, string currentUser, bool isAdmin)
    {
        await createValidator.ValidateAndThrowAsync(request);

        // Fetch the parent cinema and do ownership check
        await GetCinemaWithOwnershipCheckAsync(request.CinemaId, currentUser, isAdmin);

        var auditorium = new Auditorium
        {
            CinemaId = request.CinemaId,
            Name = request.Name,
            MaxRows = request.MaxRows,
            MaxColumns = request.MaxColumns,
            LayoutConfiguration = new AuditoriumLayout
            {
                DisabledSeats = request.Layout.DisabledSeats.Select(s => new SeatCoordinate
                {
                    Row = s.Row, Col = s.Col
                }).ToList(),
                WheelchairSpots = request.Layout.WheelchairSpots.Select(s => new SeatCoordinate
                {
                    Row = s.Row, Col = s.Col
                }).ToList(),
                Tiers = request.Layout.Tiers.Select(t => new SeatTier
                {
                    TierName = t.TierName,
                    PriceSurcharge = t.PriceSurcharge,
                    Seats = t.Seats.Select(s => new SeatCoordinate
                    {
                        Row = s.Row, Col = s.Col
                    }).ToList()
                }).ToList()
            }
        };

        dbContext.Auditoriums.Add(auditorium);
        await dbContext.SaveChangesAsync();

        return MapToResponse(auditorium);
    }

    public async Task UpdateAuditoriumAsync(Guid id, UpdateAuditoriumRequest request, string currentUser, bool isAdmin)
    {
        await updateValidator.ValidateAndThrowAsync(request);

        // Fetch the parent cinema and do ownership check
        var auditorium = await GetAuditoriumWithOwnershipCheckAsync(id, currentUser, isAdmin);

        auditorium.Name = request.Name;
        auditorium.MaxRows = request.MaxRows;
        auditorium.MaxColumns = request.MaxColumns;

        auditorium.LayoutConfiguration = new AuditoriumLayout
        {
            DisabledSeats = request.Layout.DisabledSeats.Select(s => new SeatCoordinate
            {
                Row = s.Row, Col = s.Col
            }).ToList(),
            WheelchairSpots = request.Layout.WheelchairSpots.Select(s => new SeatCoordinate
            {
                Row = s.Row, Col = s.Col
            }).ToList(),
            Tiers = request.Layout.Tiers.Select(t => new SeatTier
            {
                TierName = t.TierName,
                PriceSurcharge = t.PriceSurcharge,
                Seats = t.Seats.Select(s => new SeatCoordinate
                {
                    Row = s.Row, Col = s.Col
                }).ToList()
            }).ToList()
        };

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAuditoriumAsync(Guid id, string currentUser, bool isAdmin)
    {
        // Fetch the parent cinema and do ownership check
        var auditorium = await GetAuditoriumWithOwnershipCheckAsync(id, currentUser, isAdmin);

        dbContext.Auditoriums.Remove(auditorium);
        await dbContext.SaveChangesAsync();
    }

    // A private helper to keep the mapping logic DRY
    private static AuditoriumResponse MapToResponse(Auditorium auditorium)
    {
        var layoutDto = new AuditoriumLayoutDto(
            auditorium.LayoutConfiguration.DisabledSeats.Select(s => new SeatCoordinateDto(s.Row, s.Col)).ToList(),
            auditorium.LayoutConfiguration.WheelchairSpots.Select(s => new SeatCoordinateDto(s.Row, s.Col)).ToList(),
            auditorium.LayoutConfiguration.Tiers.Select(t => new SeatTierDto(
                t.TierName,
                t.PriceSurcharge,
                t.Seats.Select(s => new SeatCoordinateDto(s.Row, s.Col)).ToList()
            )).ToList()
        );

        return new AuditoriumResponse(
            auditorium.Id, auditorium.CinemaId, auditorium.Name, auditorium.MaxRows, auditorium.MaxColumns, layoutDto);
    }

    private async Task<Cinema> GetCinemaWithOwnershipCheckAsync(
        Guid cinemaId,
        string currentUser,
        bool isAdmin)
    {
        var cinema = await dbContext.Cinemas
                .FirstOrDefaultAsync(c => c.Id == cinemaId)
            ?? throw new KeyNotFoundException("The specified Cinema ID does not exist.");

        if (!isAdmin && cinema.OrganizerId != currentUser)
            throw new UnauthorizedAccessException("You do not own this cinema.");

        return cinema;
    }

    private async Task<Auditorium> GetAuditoriumWithOwnershipCheckAsync(
        Guid auditoriumId,
        string currentUser,
        bool isAdmin)
    {
        var auditorium = await dbContext.Auditoriums
                .Include(a => a.Cinema)
                .FirstOrDefaultAsync(a => a.Id == auditoriumId)
            ?? throw new KeyNotFoundException($"Auditorium with ID {auditoriumId} not found.");

        if (!isAdmin && auditorium.Cinema!.OrganizerId != currentUser)
            throw new UnauthorizedAccessException("You do not own this cinema.");

        return auditorium;
    }
}