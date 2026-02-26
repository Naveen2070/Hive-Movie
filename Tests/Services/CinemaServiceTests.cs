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
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);

        var result = await service.GetAllCinemasAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllCinemasAsync_ReturnsAllActiveCinemas()
    {
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

        var result = (await service.GetAllCinemasAsync()).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Name == "Cinema A");
        Assert.Contains(result, c => c.Name == "Cinema B");
    }

    // --- 2. TESTS FOR: GetCinemaByIdAsync ---

    [Fact]
    public async Task GetCinemaByIdAsync_ReturnsCinema_WhenIdExists()
    {
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

        var result = await service.GetCinemaByIdAsync(expectedId);

        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Id);
        Assert.Equal("Hive IMAX", result.Name);
    }

    [Fact]
    public async Task GetCinemaByIdAsync_ThrowsKeyNotFound_WhenIdDoesNotExist()
    {
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetCinemaByIdAsync(Guid.NewGuid()));
        Assert.Contains("not found", ex.Message);
    }

    // --- 3. TESTS FOR: CreateCinemaAsync ---

    [Fact]
    public async Task CreateCinemaAsync_ValidRequest_SavesToDatabase_AndForcesPendingStatus()
    {
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);

        var request = new CreateCinemaRequest("New Cinema", "Downtown", "contact@cinema.com");
        const string organizerId = "Organizer-99";

        var result = await service.CreateCinemaAsync(request, organizerId);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("New Cinema", result.Name);
        Assert.Equal(nameof(CinemaApprovalStatus.Pending), result.ApprovalStatus);

        var savedCinema = await dbContext.Cinemas.FindAsync(result.Id);
        Assert.NotNull(savedCinema);
        Assert.Equal(organizerId, savedCinema.OrganizerId);
        Assert.Equal(CinemaApprovalStatus.Pending, savedCinema.ApprovalStatus);
    }

    // --- 4. TESTS FOR: UpdateCinemaAsync ---

    [Fact]
    public async Task UpdateCinemaAsync_ValidOwner_UpdatesNameAndLocation()
    {
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

        // Act (Passed "Org-1" as the currentUser, isAdmin = false)
        await service.UpdateCinemaAsync(cinemaId, request, "Org-1", false);

        var updatedCinema = await dbContext.Cinemas.FindAsync(cinemaId);
        Assert.Equal("New Name", updatedCinema!.Name);
        Assert.Equal("New Loc", updatedCinema.Location);
    }

    [Fact]
    public async Task UpdateCinemaAsync_WrongOwner_ThrowsUnauthorizedAccessException()
    {
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);
        var cinemaId = Guid.NewGuid();

        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "Name",
            Location = "Loc",
            OrganizerId = "RealOwner",
            ContactEmail = "test@hive.com"
        });
        await dbContext.SaveChangesAsync();

        var request = new UpdateCinemaRequest("Hacked Name", "Hacked Loc");

        // Act & Assert (Hacker tries to update)
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateCinemaAsync(cinemaId, request, "Hacker", false));
        Assert.Equal("You do not own this cinema.", ex.Message);
    }

    [Fact]
    public async Task UpdateCinemaAsync_SuperAdmin_OverridesOwnershipCheck()
    {
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);
        var cinemaId = Guid.NewGuid();

        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "Name",
            Location = "Loc",
            OrganizerId = "RealOwner",
            ContactEmail = "test@hive.com"
        });
        await dbContext.SaveChangesAsync();

        var request = new UpdateCinemaRequest("Admin Updated", "Loc");

        // Act (Admin doesn't own it, but isAdmin = true)
        await service.UpdateCinemaAsync(cinemaId, request, "AdminUser", true);

        var updatedCinema = await dbContext.Cinemas.FindAsync(cinemaId);
        Assert.Equal("Admin Updated", updatedCinema!.Name);
    }

    [Fact]
    public async Task UpdateCinemaAsync_WhenCinemaDoesNotExist_ThrowsKeyNotFound()
    {
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);
        var request = new UpdateCinemaRequest("Name", "Loc");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateCinemaAsync(Guid.NewGuid(), request, "Org-1", false));
    }

    // --- 5. TESTS FOR: UpdateCinemaStatusAsync (Admin Action) ---

    [Fact]
    public async Task UpdateCinemaStatusAsync_WhenCinemaExists_UpdatesStatusSuccessfully()
    {
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

        await service.UpdateCinemaStatusAsync(cinemaId, CinemaApprovalStatus.Approved);

        var updatedCinema = await dbContext.Cinemas.FindAsync(cinemaId);
        Assert.Equal(CinemaApprovalStatus.Approved, updatedCinema!.ApprovalStatus);
    }

    [Fact]
    public async Task UpdateCinemaStatusAsync_WhenCinemaDoesNotExist_ThrowsKeyNotFound()
    {
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateCinemaStatusAsync(Guid.NewGuid(), CinemaApprovalStatus.Approved));
    }

    // --- 6. TESTS FOR: DeleteCinemaAsync ---

    [Fact]
    public async Task DeleteCinemaAsync_ValidOwner_SoftDeletesFromDatabase()
    {
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
        await service.DeleteCinemaAsync(cinemaId, "Org-1", false);

        var softDeletedCinema = await dbContext.Cinemas.FindAsync(cinemaId);
        Assert.NotNull(softDeletedCinema);
        Assert.True(softDeletedCinema.IsDeleted);
        Assert.NotNull(softDeletedCinema.DeletedAtUtc);

        var visibleCinemas = await dbContext.Cinemas.ToListAsync();
        Assert.Empty(visibleCinemas);
    }

    [Fact]
    public async Task DeleteCinemaAsync_WrongOwner_ThrowsUnauthorizedAccessException()
    {
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);
        var cinemaId = Guid.NewGuid();

        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "Cinema",
            Location = "Loc",
            OrganizerId = "RealOwner",
            ContactEmail = "doom@hive.com"
        });
        await dbContext.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteCinemaAsync(cinemaId, "Hacker", false));
        Assert.Equal("You do not own this cinema.", ex.Message);
    }

    [Fact]
    public async Task DeleteCinemaAsync_SuperAdmin_OverridesOwnershipCheck()
    {
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);
        var cinemaId = Guid.NewGuid();

        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "Cinema",
            Location = "Loc",
            OrganizerId = "RealOwner",
            ContactEmail = "doom@hive.com"
        });
        await dbContext.SaveChangesAsync();

        // Act
        await service.DeleteCinemaAsync(cinemaId, "AdminUser", true);

        var softDeletedCinema = await dbContext.Cinemas.FindAsync(cinemaId);
        Assert.True(softDeletedCinema!.IsDeleted);
    }

    [Fact]
    public async Task DeleteCinemaAsync_WhenCinemaDoesNotExist_ThrowsKeyNotFound()
    {
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new CinemaService(dbContext);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteCinemaAsync(Guid.NewGuid(), "Org-1", false));
    }
}