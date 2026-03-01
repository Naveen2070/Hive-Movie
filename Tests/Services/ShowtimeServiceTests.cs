using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.CurrentUser;
using Hive_Movie.Services.ShowTimes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
namespace Tests.Services;

public class ShowtimeServiceTests
{
    // Helper: Pristine In-Memory Database and Real MemoryCache
    private static (ApplicationDbContext DbContext, IMemoryCache Cache) GetTestInfrastructure(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(u => u.UserId).Returns("TestUser-123");

        var dbContext = new ApplicationDbContext(options, mockUserService.Object);
        var cache = new MemoryCache(new MemoryCacheOptions());

        return (dbContext, cache);
    }

    // Helper: Seeds a full valid hierarchy
    private async static Task<(Movie Movie, Cinema Cinema, Auditorium Auditorium, Showtime Showtime)> SeedDataAsync(
        ApplicationDbContext dbContext,
        string organizerId,
        CinemaApprovalStatus approvalStatus = CinemaApprovalStatus.Approved)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(), Title = "Inception", Description = "A movie about dreams", DurationMinutes = 148
        };

        var cinema = new Cinema
        {
            Id = Guid.NewGuid(),
            Name = "Hive IMAX",
            ContactEmail = "test@hive.com",
            Location = "123 Main St, Hive City",
            OrganizerId = organizerId,
            ApprovalStatus = approvalStatus
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

        var showtime = new Showtime
        {
            Id = Guid.NewGuid(),
            MovieId = movie.Id,
            AuditoriumId = auditorium.Id,
            StartTimeUtc = DateTime.UtcNow.AddDays(1),
            BasePrice = 15.00m,
            SeatAvailabilityState = new byte[100] // 100 empty seats
        };

        dbContext.Movies.Add(movie);
        dbContext.Cinemas.Add(cinema);
        dbContext.Auditoriums.Add(auditorium);
        dbContext.Showtimes.Add(showtime);
        await dbContext.SaveChangesAsync();

        return (movie, cinema, auditorium, showtime);
    }

    // --- 1. TESTS FOR: GetSeatMapAsync & Caching ---

    [Fact]
    public async Task GetSeatMapAsync_ShouldFetchFromDb_AndStoreInCache()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new ShowtimeService(dbContext, cache);
        var data = await SeedDataAsync(dbContext, "Org-1");

        // Act
        var result = await service.GetSeatMapAsync(data.Showtime.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Inception", result.MovieTitle);
        Assert.Equal(100, result.SeatMap.Count);

        // Verify it was saved to the RAM Cache
        Assert.True(cache.TryGetValue($"SeatMap_{data.Showtime.Id}", out ShowtimeSeatMapResponse? cachedMap));
        Assert.NotNull(cachedMap);
    }

    [Fact]
    public async Task GetSeatMapAsync_ShouldFetchFromCache_IfAvailable()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new ShowtimeService(dbContext, cache);
        var showtimeId = Guid.NewGuid();

        // Inject a FAKE response directly into the cache (DB is completely empty!)
        var fakeResponse = new ShowtimeSeatMapResponse("Cached Movie", "Cached Cinema", "A1", 10, 10, 15.00m,
            [], []);
        cache.Set($"SeatMap_{showtimeId}", fakeResponse);

        // Act
        var result = await service.GetSeatMapAsync(showtimeId);

        // Assert
        // If it returns "Cached Movie", we know for a fact it skipped the database query entirely.
        Assert.Equal("Cached Movie", result.MovieTitle);
    }


    // --- 2. TESTS FOR: Security & Ownership (Create, Update, Delete) ---

    [Fact]
    public async Task CreateShowtimeAsync_ValidOwnerAndApprovedCinema_CreatesSuccessfully()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new ShowtimeService(dbContext, cache);
        var data = await SeedDataAsync(dbContext, "OwnerUser");

        var request = new CreateShowtimeRequest(data.Movie.Id, data.Auditorium.Id, DateTime.UtcNow, 20.00m);

        // Act
        var result = await service.CreateShowtimeAsync(request, "OwnerUser", false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(20.00m, result.BasePrice);
        Assert.Equal(2, await dbContext.Showtimes.CountAsync()); // 1 from seed, 1 new
    }

    [Fact]
    public async Task CreateShowtimeAsync_PendingCinema_ThrowsInvalidOperation()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new ShowtimeService(dbContext, cache);
        // Seed the cinema as PENDING
        var data = await SeedDataAsync(dbContext, "OwnerUser", CinemaApprovalStatus.Pending);

        var request = new CreateShowtimeRequest(data.Movie.Id, data.Auditorium.Id, DateTime.UtcNow, 20.00m);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateShowtimeAsync(request, "OwnerUser", false));

        Assert.Contains("not been approved by an Admin", ex.Message);
    }

    [Fact]
    public async Task UpdateShowtimeAsync_WrongOwner_ThrowsUnauthorizedAccess()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new ShowtimeService(dbContext, cache);
        var data = await SeedDataAsync(dbContext, "RealOwner");

        var request = new UpdateShowtimeRequest(DateTime.UtcNow, 25.00m);

        // Act & Assert
        // HackerUser is trying to update RealOwner's showtime!
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateShowtimeAsync(data.Showtime.Id, request, "HackerUser", false));

        Assert.Equal("You do not own the cinema running this showtime.", ex.Message);
    }

    [Fact]
    public async Task DeleteShowtimeAsync_Admin_CanDeleteAnyShowtime()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new ShowtimeService(dbContext, cache);
        var data = await SeedDataAsync(dbContext, "RealOwner");

        // Act
        // Admin is deleting it (isAdmin = true), even though their username is "SuperAdmin"
        await service.DeleteShowtimeAsync(data.Showtime.Id, "SuperAdmin", true);

        // Assert
        Assert.Empty(await dbContext.Showtimes.ToListAsync());
    }

    // --- 3. TESTS FOR: GetShowtimesByMovieIdAsync (Catalog Browsing) ---

    [Fact]
    public async Task GetShowtimesByMovieId_ShouldReturnOnlyFutureShowtimes_OrderedChronologically()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new ShowtimeService(dbContext, cache);
        var data = await SeedDataAsync(dbContext, "Org-1");

        // Add a past showtime (should be filtered out)
        dbContext.Showtimes.Add(new Showtime
        {
            Id = Guid.NewGuid(),
            MovieId = data.Movie.Id,
            AuditoriumId = data.Auditorium.Id,
            StartTimeUtc = DateTime.UtcNow.AddDays(-1),
            BasePrice = 10m,
            SeatAvailabilityState = new byte[10]
        });

        // Add a showtime further in the future to test sorting
        dbContext.Showtimes.Add(new Showtime
        {
            Id = Guid.NewGuid(),
            MovieId = data.Movie.Id,
            AuditoriumId = data.Auditorium.Id,
            StartTimeUtc = DateTime.UtcNow.AddDays(5),
            BasePrice = 10m,
            SeatAvailabilityState = new byte[10]
        });

        await dbContext.SaveChangesAsync();

        // Act
        var result = (await service.GetShowtimesByMovieIdAsync(data.Movie.Id)).ToList();

        // Assert
        Assert.Equal(2, result.Count); // 1 from seed (future), 1 new future, 1 past (ignored)
        Assert.True(result[0].StartTimeUtc < result[1].StartTimeUtc); // Ensures chronological ordering
    }

    [Fact]
    public async Task GetShowtimesByMovieId_ShouldExcludeSoftDeletedShowtimes()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new ShowtimeService(dbContext, cache);
        var data = await SeedDataAsync(dbContext, "Org-1");

        // Mark the seeded future showtime as soft-deleted
        data.Showtime.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetShowtimesByMovieIdAsync(data.Movie.Id);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetShowtimesByMovieId_NonExistentMovie_ReturnsEmptyList()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new ShowtimeService(dbContext, cache);

        // Act
        var result = await service.GetShowtimesByMovieIdAsync(Guid.NewGuid());

        // Assert
        Assert.Empty(result);
    }
}