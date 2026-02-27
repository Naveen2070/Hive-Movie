using Hive_Movie.Controllers;
using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.CurrentUser;
using Hive_Movie.Services.ShowTimes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System.Security.Claims;
namespace Tests.Integration.Controllers;

[Collection("Database collection")]
public class ShowtimesControllerTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    // --- TEST INFRASTRUCTURE HELPERS ---

    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;

        var mockUser = new Mock<ICurrentUserService>();
        return new ApplicationDbContext(options, mockUser.Object);
    }

    private ShowtimesController CreateController(ApplicationDbContext dbContext, string? userId, string role)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ShowtimeService(dbContext, cache);
        var controller = new ShowtimesController(service);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role)
        };
        if (userId != null) claims.Add(new Claim("id", userId));

        var identity = new ClaimsIdentity(claims, "TestAuth");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return controller;
    }

    // Helper to seed the massive required hierarchy (Movie -> Cinema -> Auditorium)
    private async Task<(Movie movie, Cinema cinema, Auditorium auditorium)> SeedHierarchyAsync(
        ApplicationDbContext dbContext,
        string organizerId,
        CinemaApprovalStatus status = CinemaApprovalStatus.Approved)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(), Title = "Test Movie", Description = "Desc", DurationMinutes = 120
        };
        var cinema = new Cinema
        {
            Id = Guid.NewGuid(),
            Name = "Test Cinema",
            ContactEmail = "test@hive.com",
            Location = "Loc",
            OrganizerId = organizerId,
            ApprovalStatus = status
        };
        var auditorium = new Auditorium
        {
            Id = Guid.NewGuid(),
            CinemaId = cinema.Id,
            Name = "Screen 1",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        };

        dbContext.Movies.Add(movie);
        dbContext.Cinemas.Add(cinema);
        dbContext.Auditoriums.Add(auditorium);
        await dbContext.SaveChangesAsync();

        return (movie, cinema, auditorium);
    }

    // --- 1. SEAT MAP & RESERVATIONS (PUBLIC/USER ENDPOINTS) ---

    [Fact]
    public async Task GetSeatMap_ExistingShowtime_ReturnsOkWithMap()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var data = await SeedHierarchyAsync(dbContext, "Org-1");

        var showtime = new Showtime
        {
            Id = Guid.NewGuid(),
            MovieId = data.movie.Id,
            AuditoriumId = data.auditorium.Id,
            StartTimeUtc = DateTime.UtcNow,
            BasePrice = 10,
            SeatAvailabilityState = new byte[100]
        };
        dbContext.Showtimes.Add(showtime);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "Guest", "ROLE_USER");

        // Act
        var result = await controller.GetSeatMap(showtime.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ShowtimeSeatMapResponse>(okResult.Value);
        Assert.Equal(100, response.SeatMap.Count);
        Assert.Equal("Test Movie", response.MovieTitle);
    }

    // --- 2. SHOWTIME MANAGEMENT (ORGANIZER/ADMIN ENDPOINTS) ---

    [Fact]
    public async Task Create_ValidOwnerAndApprovedCinema_ReturnsCreated()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var data = await SeedHierarchyAsync(dbContext, "Org-Owner");
        var controller = CreateController(dbContext, "Org-Owner", "ROLE_ORGANIZER");

        var request = new CreateShowtimeRequest(data.movie.Id, data.auditorium.Id, DateTime.UtcNow.AddDays(1), 15.00m);

        // Act
        var result = await controller.Create(request);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<ShowtimeResponse>(createdResult.Value);
        Assert.Equal(15.00m, response.BasePrice);
    }

    [Fact]
    public async Task Create_PendingCinema_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        // Create a cinema that is NOT approved yet
        var data = await SeedHierarchyAsync(dbContext, "Org-Owner", CinemaApprovalStatus.Pending);
        var controller = CreateController(dbContext, "Org-Owner", "ROLE_ORGANIZER");

        var request = new CreateShowtimeRequest(data.movie.Id, data.auditorium.Id, DateTime.UtcNow, 15.00m);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Create(request));
        Assert.Contains("not been approved by an Admin", ex.Message);
    }

    [Fact]
    public async Task Create_WrongOwner_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var data = await SeedHierarchyAsync(dbContext, "Real-Owner");

        // Hacker is logged in as an Organizer, but DOES NOT own this cinema
        var controller = CreateController(dbContext, "Hacker-Org", "ROLE_ORGANIZER");
        var request = new CreateShowtimeRequest(data.movie.Id, data.auditorium.Id, DateTime.UtcNow, 15.00m);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Create(request));
        Assert.Equal("You do not own the cinema this auditorium belongs to.", ex.Message);
    }

    [Fact]
    public async Task Update_ValidOwner_UpdatesAndReturnsNoContent()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var data = await SeedHierarchyAsync(dbContext, "Org-Owner");

        var showtimeId = Guid.NewGuid();
        dbContext.Showtimes.Add(new Showtime
        {
            Id = showtimeId,
            MovieId = data.movie.Id,
            AuditoriumId = data.auditorium.Id,
            StartTimeUtc = DateTime.UtcNow,
            BasePrice = 10,
            SeatAvailabilityState = new byte[100]
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "Org-Owner", "ROLE_ORGANIZER");
        var request = new UpdateShowtimeRequest(DateTime.UtcNow.AddHours(2), 20.00m);

        // Act
        var result = await controller.Update(showtimeId, request);

        // Assert
        Assert.IsType<NoContentResult>(result);
        var updated = await dbContext.Showtimes.FindAsync(showtimeId);
        Assert.Equal(20.00m, updated!.BasePrice);
    }

    [Fact]
    public async Task Delete_WrongOwner_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var data = await SeedHierarchyAsync(dbContext, "Real-Owner");

        var showtimeId = Guid.NewGuid();
        dbContext.Showtimes.Add(new Showtime
        {
            Id = showtimeId,
            MovieId = data.movie.Id,
            AuditoriumId = data.auditorium.Id,
            StartTimeUtc = DateTime.UtcNow,
            BasePrice = 10,
            SeatAvailabilityState = new byte[100]
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "Hacker-Org", "ROLE_ORGANIZER");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Delete(showtimeId));
        Assert.Equal("You do not own the cinema running this showtime.", ex.Message);
    }

    [Fact]
    public async Task Delete_SuperAdmin_OverridesOwnership_AndSoftDeletes()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var data = await SeedHierarchyAsync(dbContext, "Real-Owner");

        var showtimeId = Guid.NewGuid();
        dbContext.Showtimes.Add(new Showtime
        {
            Id = showtimeId,
            MovieId = data.movie.Id,
            AuditoriumId = data.auditorium.Id,
            StartTimeUtc = DateTime.UtcNow,
            BasePrice = 10,
            SeatAvailabilityState = new byte[100]
        });
        await dbContext.SaveChangesAsync();

        // Admin does not own the cinema, but role overrides it
        var controller = CreateController(dbContext, "Admin", "ROLE_SUPER_ADMIN");

        // Act
        var result = await controller.Delete(showtimeId);

        // Assert
        Assert.IsType<NoContentResult>(result);
        var deleted = await dbContext.Showtimes.FindAsync(showtimeId);
        Assert.True(deleted!.IsDeleted);
    }

    // --- 3. NOT FOUND & BAD INPUT EDGE CASES ---

    [Fact]
    public async Task GetSeatMap_NonExistentShowtime_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Guest", "ROLE_USER");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.GetSeatMap(Guid.NewGuid()));
        Assert.Contains("not found", ex.Message);
    }


    [Fact]
    public async Task Create_NonExistentMovie_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        // Seed the cinema and auditorium, but we'll pass a fake Movie ID
        var data = await SeedHierarchyAsync(dbContext, "Org-Owner");
        var controller = CreateController(dbContext, "Org-Owner", "ROLE_ORGANIZER");

        var fakeMovieId = Guid.NewGuid();
        var request = new CreateShowtimeRequest(fakeMovieId, data.auditorium.Id, DateTime.UtcNow.AddDays(1), 15.00m);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.Create(request));
        Assert.Equal("The specified Movie ID does not exist.", ex.Message);
    }

    [Fact]
    public async Task Update_NonExistentShowtime_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Org-Owner", "ROLE_ORGANIZER");
        var request = new UpdateShowtimeRequest(DateTime.UtcNow.AddHours(2), 20.00m);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.Update(Guid.NewGuid(), request));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task Delete_AlreadySoftDeletedShowtime_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var data = await SeedHierarchyAsync(dbContext, "Org-Owner");

        var showtime = new Showtime
        {
            Id = Guid.NewGuid(),
            MovieId = data.movie.Id,
            AuditoriumId = data.auditorium.Id,
            StartTimeUtc = DateTime.UtcNow,
            BasePrice = 10,
            SeatAvailabilityState = new byte[100]
            // DO NOT set IsDeleted here! The interceptor will just overwrite it.
        };
        dbContext.Showtimes.Add(showtime);
        await dbContext.SaveChangesAsync(); // Saved normally (IsDeleted = false)

        //  we explicitly tell EF Core to delete it!
        // This triggers the EntityState.Deleted case in your interceptor, properly setting IsDeleted = true
        dbContext.Showtimes.Remove(showtime);
        await dbContext.SaveChangesAsync();

        // Clear the cache so the controller is forced to hit the SQL database
        dbContext.ChangeTracker.Clear();

        var controller = CreateController(dbContext, "Org-Owner", "ROLE_ORGANIZER");

        // Act & Assert
        // The record is now truly soft-deleted in the DB. The Global Query filter will hide it!
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.Delete(showtime.Id));
        Assert.Contains("not found", ex.Message);
    }
}