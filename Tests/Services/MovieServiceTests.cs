using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.CurrentUser;
using Hive_Movie.Services.Movies;
using Microsoft.EntityFrameworkCore;
using Moq;
namespace Tests.Services;

public class MovieServiceTests
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

    // --- 1. TESTS FOR: GetAllMoviesAsync ---

    [Fact]
    public async Task GetAllMoviesAsync_ReturnsEmptyList_WhenNoMoviesExist()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new MovieService(dbContext);

        // Act
        var result = await service.GetAllMoviesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // Validates it doesn't return null or crash
    }

    [Fact]
    public async Task GetAllMoviesAsync_ReturnsMoviesOrderedByReleaseDateDescending()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new MovieService(dbContext);

        var oldMovie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Old Movie",
            Description = "Old Description",
            ReleaseDate = new DateTime(2000, 1, 1),
            DurationMinutes = 120
        };
        var newMovie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "New Movie",
            Description = "New Description",
            ReleaseDate = new DateTime(2025, 1, 1),
            DurationMinutes = 120
        };
        var midMovie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Mid Movie",
            Description = "Mid Description",
            ReleaseDate = new DateTime(2010, 1, 1),
            DurationMinutes = 120
        };

        dbContext.Movies.AddRange(oldMovie, newMovie, midMovie);
        await dbContext.SaveChangesAsync();

        // Act
        var result = (await service.GetAllMoviesAsync()).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        // The newest movie (2025) should be first!
        Assert.Equal("New Movie", result[0].Title);
        Assert.Equal("Mid Movie", result[1].Title);
        Assert.Equal("Old Movie", result[2].Title);
    }

    // --- 2. TESTS FOR: GetMovieByIdAsync ---

    [Fact]
    public async Task GetMovieByIdAsync_ReturnsMovie_WhenIdExists()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new MovieService(dbContext);
        var expectedMovieId = Guid.NewGuid();

        dbContext.Movies.Add(new Movie
        {
            Id = expectedMovieId, Title = "The Matrix", Description = "A movie about a man who is a computer programmer", DurationMinutes = 136
        });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetMovieByIdAsync(expectedMovieId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedMovieId, result.Id);
        Assert.Equal("The Matrix", result.Title);
    }

    [Fact]
    public async Task GetMovieByIdAsync_ThrowsKeyNotFound_WhenIdDoesNotExist()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new MovieService(dbContext);
        var fakeId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetMovieByIdAsync(fakeId));
        Assert.Equal($"Movie with ID {fakeId} not found.", ex.Message);
    }

    // --- 3. TESTS FOR: CreateMovieAsync ---

    [Fact]
    public async Task CreateMovieAsync_ValidRequest_SavesToDatabaseAndReturnsResponse()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new MovieService(dbContext);

        var request = new CreateMovieRequest(
            "Inception",
            "A thief who steals corporate secrets...",
            148,
            new DateTime(2010, 7, 16),
            "https://poster.url"
        );

        // Act
        var result = await service.CreateMovieAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id); // EF Core should have generated a new Guid
        Assert.Equal(request.Title, result.Title);

        // Verify it was actually committed to the InMemory Database
        var savedMovie = await dbContext.Movies.FindAsync(result.Id);
        Assert.NotNull(savedMovie);
        Assert.Equal("Inception", savedMovie.Title);
    }

    // --- 4. TESTS FOR: UpdateMovieAsync ---

    [Fact]
    public async Task UpdateMovieAsync_WhenMovieExists_UpdatesPropertiesSuccessfully()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new MovieService(dbContext);
        var movieId = Guid.NewGuid();

        var originalMovie = new Movie
        {
            Id = movieId, Title = "Old Title", Description = "Old Description", DurationMinutes = 90
        };
        dbContext.Movies.Add(originalMovie);
        await dbContext.SaveChangesAsync();

        var request = new UpdateMovieRequest(
            "New Title",
            "Updated Description",
            120,
            new DateTime(2025, 1, 1),
            "https://newposter.url"
        );

        // Act
        await service.UpdateMovieAsync(movieId, request);

        // Assert
        var updatedMovie = await dbContext.Movies.FindAsync(movieId);
        Assert.NotNull(updatedMovie);
        Assert.Equal("New Title", updatedMovie.Title);
        Assert.Equal(120, updatedMovie.DurationMinutes);
        Assert.Equal("Updated Description", updatedMovie.Description);
    }

    [Fact]
    public async Task UpdateMovieAsync_WhenMovieDoesNotExist_ThrowsKeyNotFound()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new MovieService(dbContext);
        var fakeId = Guid.NewGuid();

        var request = new UpdateMovieRequest("Title", "Desc", 120, DateTime.UtcNow, "url");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateMovieAsync(fakeId, request));
        Assert.Equal($"Movie with ID {fakeId} not found.", ex.Message);
    }

    // --- 5. TESTS FOR: DeleteMovieAsync ---

    [Fact]
    public async Task DeleteMovieAsync_WhenMovieExists_SoftDeletesFromDatabase()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new MovieService(dbContext);
        var movieId = Guid.NewGuid();

        dbContext.Movies.Add(new Movie
        {
            Id = movieId, Title = "To Be Deleted", Description = "To Be Deleted", DurationMinutes = 100
        });
        await dbContext.SaveChangesAsync();

        // Act
        await service.DeleteMovieAsync(movieId);

        // Assert 1: The entity still exists in the tracker, but it should be marked as deleted!
        var softDeletedMovie = await dbContext.Movies.FindAsync(movieId);
        Assert.NotNull(softDeletedMovie); // It's still in the DB!
        Assert.True(softDeletedMovie.IsDeleted); // But it is marked as dead
        Assert.NotNull(softDeletedMovie.DeletedAtUtc);

        // Assert 2: Prove that the Global Query Filter (m => !m.IsDeleted) successfully hides it!
        // ToListAsync() forces EF Core to run a real query, which will apply the filter.
        var visibleMovies = await dbContext.Movies.ToListAsync();
        Assert.Empty(visibleMovies);
    }

    [Fact]
    public async Task DeleteMovieAsync_WhenMovieDoesNotExist_ThrowsKeyNotFound()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext(Guid.NewGuid().ToString());
        var service = new MovieService(dbContext);
        var fakeId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteMovieAsync(fakeId));
        Assert.Equal($"Movie with ID {fakeId} not found.", ex.Message);
    }
}