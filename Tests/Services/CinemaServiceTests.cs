using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.Cinemas;
using Hive_Movie.Services.CurrentUser;
using Microsoft.EntityFrameworkCore;
using Moq;
namespace Tests.Services;

public class CinemaServiceTests
{
    // Helper: Creates a pristine, isolated In-Memory database for every single test
    private static ApplicationDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(u => u.UserId).Returns("TestUser-123");

        return new ApplicationDbContext(options, mockUserService.Object);
    }

    // --- 1. TESTS FOR: GetAllCinemasAsync ---

    [Fact]
    public async Task GetAllCinemasAsync_ReturnsEmptyList_WhenNoCinemasExist()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);

        // Act
        var result = await service.GetAllCinemasAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllCinemasAsync_ReturnsAllActiveCinemas()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);

        dbContext.Cinemas.AddRange(
            new Cinema
            {
                Id = Guid.NewGuid(),
                Name = "Cinema A",
                Location = "Loc A",
                OrganizerId = "Org-1",
                ContactEmail = "a@hive.com"
            },
            new Cinema
            {
                Id = Guid.NewGuid(),
                Name = "Cinema B",
                Location = "Loc B",
                OrganizerId = "Org-2",
                ContactEmail = "b@hive.com"
            }
        );
        await dbContext.SaveChangesAsync();

        // Act
        var result = (await service.GetAllCinemasAsync()).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Name == "Cinema A");
        Assert.Contains(result, c => c.Name == "Cinema B");
    }

    // --- 2. TESTS FOR: GetCinemaByIdAsync ---

    [Fact]
    public async Task GetCinemaByIdAsync_ReturnsCinema_WhenIdExists()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);
        var expectedId = Guid.NewGuid();

        dbContext.Cinemas.Add(new Cinema
        {
            Id = expectedId,
            Name = "Hive IMAX",
            Location = "City Center",
            OrganizerId = "Org-1",
            ContactEmail = "imax@hive.com"
        });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetCinemaByIdAsync(expectedId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Id);
        Assert.Equal("Hive IMAX", result.Name);
    }

    [Fact]
    public async Task GetCinemaByIdAsync_ThrowsKeyNotFound_WhenIdDoesNotExist()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);
        var fakeId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetCinemaByIdAsync(fakeId));
        Assert.Equal($"Cinema with ID {fakeId} not found.", ex.Message);
    }

    // --- 3. TESTS FOR: CreateCinemaAsync ---

    [Fact]
    public async Task CreateCinemaAsync_ValidRequest_SavesToDatabase_AndForcesPendingStatus()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);

        var request = new CreateCinemaRequest("New Cinema", "Downtown", "contact@cinema.com");
        const string organizerId = "Organizer-99";

        // Act
        var result = await service.CreateCinemaAsync(request, organizerId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id); // EF Core generated a Guid
        Assert.Equal("New Cinema", result.Name);
        Assert.Equal(nameof(CinemaApprovalStatus.Pending), result.ApprovalStatus); // Ensure it mapped correctly to the DTO

        // Verify it was actually committed to the database correctly
        var savedCinema = await dbContext.Cinemas.FindAsync(result.Id);
        Assert.NotNull(savedCinema);
        Assert.Equal(organizerId, savedCinema.OrganizerId);
        Assert.Equal(CinemaApprovalStatus.Pending, savedCinema.ApprovalStatus); // Critical Business Rule Check!
    }

    // --- 4. TESTS FOR: UpdateCinemaAsync ---

    [Fact]
    public async Task UpdateCinemaAsync_WhenCinemaExists_UpdatesNameAndLocation()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);
        var cinemaId = Guid.NewGuid();

        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "Old Name",
            Location = "Old Loc",
            OrganizerId = "Org-1",
            ContactEmail = "test@hive.com"
        });
        await dbContext.SaveChangesAsync();

        var request = new UpdateCinemaRequest("New Name", "New Loc");

        // Act
        await service.UpdateCinemaAsync(cinemaId, request);

        // Assert
        var updatedCinema = await dbContext.Cinemas.FindAsync(cinemaId);
        Assert.NotNull(updatedCinema);
        Assert.Equal("New Name", updatedCinema.Name);
        Assert.Equal("New Loc", updatedCinema.Location);
    }

    [Fact]
    public async Task UpdateCinemaAsync_WhenCinemaDoesNotExist_ThrowsKeyNotFound()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);
        var request = new UpdateCinemaRequest("Name", "Loc");

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateCinemaAsync(Guid.NewGuid(), request));
    }

    // --- 5. TESTS FOR: UpdateCinemaStatusAsync (Admin Action) ---

    [Fact]
    public async Task UpdateCinemaStatusAsync_WhenCinemaExists_UpdatesStatusSuccessfully()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);
        var cinemaId = Guid.NewGuid();

        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "Test Cinema",
            Location = "Test Loc",
            OrganizerId = "Org-1",
            ContactEmail = "test@hive.com",
            ApprovalStatus = CinemaApprovalStatus.Pending
        });
        await dbContext.SaveChangesAsync();

        // Act
        await service.UpdateCinemaStatusAsync(cinemaId, CinemaApprovalStatus.Approved);

        // Assert
        var updatedCinema = await dbContext.Cinemas.FindAsync(cinemaId);
        Assert.NotNull(updatedCinema);
        Assert.Equal(CinemaApprovalStatus.Approved, updatedCinema.ApprovalStatus);
    }

    [Fact]
    public async Task UpdateCinemaStatusAsync_WhenCinemaDoesNotExist_ThrowsKeyNotFound()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateCinemaStatusAsync(Guid.NewGuid(), CinemaApprovalStatus.Approved));
    }

    // --- 6. TESTS FOR: DeleteCinemaAsync ---

    [Fact]
    public async Task DeleteCinemaAsync_WhenCinemaExists_SoftDeletesFromDatabase()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);
        var cinemaId = Guid.NewGuid();

        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "Doomed Cinema",
            Location = "Nowhere",
            OrganizerId = "Org-1",
            ContactEmail = "doom@hive.com"
        });
        await dbContext.SaveChangesAsync();

        // Act
        await service.DeleteCinemaAsync(cinemaId);

        // Assert 1: The entity still exists in the local tracker, but is marked as deleted!
        var softDeletedCinema = await dbContext.Cinemas.FindAsync(cinemaId);
        Assert.NotNull(softDeletedCinema);
        Assert.True(softDeletedCinema.IsDeleted);
        Assert.NotNull(softDeletedCinema.DeletedAtUtc);

        // Assert 2: Prove that the Global Query Filter (c => !c.IsDeleted) successfully hides it
        var visibleCinemas = await dbContext.Cinemas.ToListAsync();
        Assert.Empty(visibleCinemas);
    }

    [Fact]
    public async Task DeleteCinemaAsync_WhenCinemaDoesNotExist_ThrowsKeyNotFound()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteCinemaAsync(Guid.NewGuid()));
    }
}