using FluentValidation;
using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.Auditoriums;
using Hive_Movie.Services.CurrentUser;
using Hive_Movie.Validators.Auditorium;
using Microsoft.EntityFrameworkCore;
using Moq;
namespace Tests.Services;

public class AuditoriumServiceTests
{
    // Helper: Creates a pristine In-Memory database
    private static ApplicationDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(u => u.UserId).Returns("TestUser-123");

        return new ApplicationDbContext(options, mockUserService.Object);
    }

    // Helper: Creates a service with real validators to ensure end-to-end integration
    private static AuditoriumService CreateService(ApplicationDbContext dbContext)
    {
        var createValidator = new CreateAuditoriumRequestValidator();
        var updateValidator = new UpdateAuditoriumRequestValidator();
        return new AuditoriumService(dbContext, createValidator, updateValidator);
    }

    // Helper: Seeds a valid Cinema
    private async static Task<Cinema> SeedCinemaAsync(ApplicationDbContext dbContext, string organizerId)
    {
        var cinema = new Cinema
        {
            Id = Guid.NewGuid(),
            Name = "Test Cinema",
            ContactEmail = "test@hive.com",
            Location = "Test Loc",
            OrganizerId = organizerId
        };
        dbContext.Cinemas.Add(cinema);
        await dbContext.SaveChangesAsync();
        return cinema;
    }

    // --- 1. TESTS FOR: Read Operations (Get All, By Cinema, By Id) ---

    [Fact]
    public async Task GetAllAuditoriumsAsync_ReturnsMappedAuditoriums()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = CreateService(dbContext);
        var cinema = await SeedCinemaAsync(dbContext, "Org-1");

        dbContext.Auditoriums.Add(new Auditorium
        {
            Id = Guid.NewGuid(),
            CinemaId = cinema.Id,
            Name = "A1",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        });
        dbContext.Auditoriums.Add(new Auditorium
        {
            Id = Guid.NewGuid(),
            CinemaId = cinema.Id,
            Name = "A2",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        });
        await dbContext.SaveChangesAsync();

        // Act
        var result = (await service.GetAllAuditoriumsAsync()).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Name == "A1");
        Assert.Contains(result, a => a.Name == "A2");
    }

    [Fact]
    public async Task GetAuditoriumsByCinemaIdAsync_FiltersCorrectly()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = CreateService(dbContext);

        var cinema1 = await SeedCinemaAsync(dbContext, "Org-1");
        var cinema2 = await SeedCinemaAsync(dbContext, "Org-2");

        dbContext.Auditoriums.Add(new Auditorium
        {
            Id = Guid.NewGuid(),
            CinemaId = cinema1.Id,
            Name = "Cinema1-Screen",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        });
        dbContext.Auditoriums.Add(new Auditorium
        {
            Id = Guid.NewGuid(),
            CinemaId = cinema2.Id,
            Name = "Cinema2-Screen",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        });
        await dbContext.SaveChangesAsync();

        // Act
        var result = (await service.GetAuditoriumsByCinemaIdAsync(cinema1.Id)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Cinema1-Screen", result[0].Name);
    }

    [Fact]
    public async Task GetAuditoriumByIdAsync_ThrowsKeyNotFound_WhenMissing()
    {
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAuditoriumByIdAsync(Guid.NewGuid()));
    }

    // --- 2. TESTS FOR: CreateAuditoriumAsync ---

    [Fact]
    public async Task CreateAuditoriumAsync_ValidRequestOwner_CreatesAndMapsLayoutSuccessfully()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = CreateService(dbContext);
        const string organizerId = "Org-Owner";
        var cinema = await SeedCinemaAsync(dbContext, organizerId);

        var layoutDto = new AuditoriumLayoutDto(
            [new SeatCoordinateDto(0, 0)],
            [],
            [
                new SeatTierDto("VIP", 5.0m, [new SeatCoordinateDto(5, 5)])
            ]
        );
        var request = new CreateAuditoriumRequest(cinema.Id, "IMAX", 10, 10, layoutDto);

        // Act
        var result = await service.CreateAuditoriumAsync(request, organizerId, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("IMAX", result.Name);
        Assert.Single(result.Layout.DisabledSeats);
        Assert.Single(result.Layout.Tiers);
        Assert.Equal("VIP", result.Layout.Tiers[0].TierName);

        var savedAuditorium = await dbContext.Auditoriums.FindAsync(result.Id);
        Assert.NotNull(savedAuditorium);
    }

    [Fact]
    public async Task CreateAuditoriumAsync_ValidationError_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = CreateService(dbContext);

        // Invalid: MaxRows is 0! (FluentValidation should catch this before EF Core even tries)
        var request = new CreateAuditoriumRequest(Guid.NewGuid(), "Bad Room", 0, 10, new AuditoriumLayoutDto([], [], []));

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAuditoriumAsync(request, "Org-1", false));
    }

    [Fact]
    public async Task CreateAuditoriumAsync_WrongOwner_ThrowsUnauthorizedAccess()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = CreateService(dbContext);
        var cinema = await SeedCinemaAsync(dbContext, "Real-Owner");

        var request = new CreateAuditoriumRequest(cinema.Id, "Hacked Room", 10, 10, new AuditoriumLayoutDto([], [], []));

        // Act & Assert (Hacker trying to add a room to Real-Owner's cinema)
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAuditoriumAsync(request, "Hacker", false));

        Assert.Equal("You do not own this cinema.", ex.Message);
    }

    [Fact]
    public async Task CreateAuditoriumAsync_AdminUser_OverridesOwnershipCheck()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = CreateService(dbContext);
        var cinema = await SeedCinemaAsync(dbContext, "Real-Owner");

        var request = new CreateAuditoriumRequest(cinema.Id, "Admin Added Room", 10, 10, new AuditoriumLayoutDto([], [], []));

        // Act (Admin adds room, even though they don't own it)
        var result = await service.CreateAuditoriumAsync(request, "SuperAdmin", true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Admin Added Room", result.Name);
    }

    // --- 3. TESTS FOR: UpdateAuditoriumAsync ---

    [Fact]
    public async Task UpdateAuditoriumAsync_ValidOwner_UpdatesSuccessfully()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = CreateService(dbContext);
        var cinema = await SeedCinemaAsync(dbContext, "Org-Owner");

        var auditoriumId = Guid.NewGuid();
        dbContext.Auditoriums.Add(new Auditorium
        {
            Id = auditoriumId,
            CinemaId = cinema.Id,
            Name = "Old Name",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        });
        await dbContext.SaveChangesAsync();

        var request = new UpdateAuditoriumRequest("New Name", 15, 15, new AuditoriumLayoutDto([], [], []));

        // Act
        await service.UpdateAuditoriumAsync(auditoriumId, request, "Org-Owner", false);

        // Assert
        var updated = await dbContext.Auditoriums.FindAsync(auditoriumId);
        Assert.NotNull(updated);
        Assert.Equal("New Name", updated.Name);
        Assert.Equal(15, updated.MaxRows);
    }

    // --- 4. TESTS FOR: DeleteAuditoriumAsync ---

    [Fact]
    public async Task DeleteAuditoriumAsync_ValidOwner_SoftDeletesFromDatabase()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = CreateService(dbContext);
        var cinema = await SeedCinemaAsync(dbContext, "Org-Owner");

        var auditoriumId = Guid.NewGuid();
        dbContext.Auditoriums.Add(new Auditorium
        {
            Id = auditoriumId,
            CinemaId = cinema.Id,
            Name = "Doomed Room",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        });
        await dbContext.SaveChangesAsync();

        // Act
        await service.DeleteAuditoriumAsync(auditoriumId, "Org-Owner", false);

        // Assert 1: The entity is in the tracker, but marked as Soft Deleted
        var softDeletedAuditorium = await dbContext.Auditoriums.FindAsync(auditoriumId);
        Assert.NotNull(softDeletedAuditorium);
        Assert.True(softDeletedAuditorium.IsDeleted);
        Assert.NotNull(softDeletedAuditorium.DeletedAtUtc);

        // Assert 2: Global Query Filter works
        var visible = await dbContext.Auditoriums.ToListAsync();
        Assert.Empty(visible);
    }

    [Fact]
    public async Task DeleteAuditoriumAsync_WrongOwner_ThrowsUnauthorizedAccess()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = CreateService(dbContext);
        var cinema = await SeedCinemaAsync(dbContext, "Real-Owner");

        var auditoriumId = Guid.NewGuid();
        dbContext.Auditoriums.Add(new Auditorium
        {
            Id = auditoriumId,
            CinemaId = cinema.Id,
            Name = "Room",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        });
        await dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteAuditoriumAsync(auditoriumId, "Hacker", false));
    }
}