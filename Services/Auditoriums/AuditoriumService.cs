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
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly IValidator<CreateAuditoriumRequest> _createValidator = createValidator;
    private readonly IValidator<UpdateAuditoriumRequest> _updateValidator = updateValidator;

    public async Task<IEnumerable<AuditoriumResponse>> GetAllAuditoriumsAsync()
    {
        var auditoriums = await _dbContext.Auditoriums.ToListAsync();
        return auditoriums.Select(MapToResponse);
    }

    public async Task<IEnumerable<AuditoriumResponse>> GetAuditoriumsByCinemaIdAsync(Guid cinemaId)
    {
        var auditoriums = await _dbContext.Auditoriums
            .Where(a => a.CinemaId == cinemaId)
            .ToListAsync();

        return auditoriums.Select(MapToResponse);
    }

    public async Task<AuditoriumResponse> GetAuditoriumByIdAsync(Guid id)
    {
        var auditorium = await _dbContext.Auditoriums.FindAsync(id);
        return auditorium == null 
            ? throw new KeyNotFoundException($"Auditorium with ID {id} not found.") 
            : MapToResponse(auditorium);
    }

    public async Task<AuditoriumResponse> CreateAuditoriumAsync(CreateAuditoriumRequest request)
    {
        await _createValidator.ValidateAndThrowAsync(request);

        if (!await _dbContext.Cinemas.AnyAsync(c => c.Id == request.CinemaId))
            throw new KeyNotFoundException("The specified Cinema ID does not exist.");

        var auditorium = new Auditorium
        {
            CinemaId = request.CinemaId,
            Name = request.Name,
            MaxRows = request.MaxRows,
            MaxColumns = request.MaxColumns,
            LayoutConfiguration = new AuditoriumLayout
            {
                DisabledSeats = request.Layout.DisabledSeats.Select(s => new SeatCoordinate { Row = s.Row, Col = s.Col }).ToList(),
                WheelchairSpots = request.Layout.WheelchairSpots.Select(s => new SeatCoordinate { Row = s.Row, Col = s.Col }).ToList()
            }
        };

        _dbContext.Auditoriums.Add(auditorium);
        await _dbContext.SaveChangesAsync();

        return MapToResponse(auditorium);
    }

    public async Task UpdateAuditoriumAsync(Guid id, UpdateAuditoriumRequest request)
    {
        await _updateValidator.ValidateAndThrowAsync(request);

        var auditorium = await _dbContext.Auditoriums.FindAsync(id)
            ?? throw new KeyNotFoundException($"Auditorium with ID {id} not found.");

        auditorium.Name = request.Name;
        auditorium.MaxRows = request.MaxRows;
        auditorium.MaxColumns = request.MaxColumns;

        // Completely overwrite the JSON layout object
        auditorium.LayoutConfiguration = new AuditoriumLayout
        {
            DisabledSeats = request.Layout.DisabledSeats.Select(s => new SeatCoordinate { Row = s.Row, Col = s.Col }).ToList(),
            WheelchairSpots = request.Layout.WheelchairSpots.Select(s => new SeatCoordinate { Row = s.Row, Col = s.Col }).ToList()
        };

        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAuditoriumAsync(Guid id)
    {
        var auditorium = await _dbContext.Auditoriums.FindAsync(id) 
            ?? throw new KeyNotFoundException($"Auditorium with ID {id} not found.");

        _dbContext.Auditoriums.Remove(auditorium);
        await _dbContext.SaveChangesAsync();
    }

    // A private helper to keep the mapping logic DRY
    private static AuditoriumResponse MapToResponse(Auditorium auditorium)
    {
        var layoutDto = new AuditoriumLayoutDto(
            auditorium.LayoutConfiguration.DisabledSeats.Select(s => new SeatCoordinateDto(s.Row, s.Col)).ToList(),
            auditorium.LayoutConfiguration.WheelchairSpots.Select(s => new SeatCoordinateDto(s.Row, s.Col)).ToList()
        );

        return new AuditoriumResponse(
            auditorium.Id, auditorium.CinemaId, auditorium.Name, auditorium.MaxRows, auditorium.MaxColumns, layoutDto);
    }
}