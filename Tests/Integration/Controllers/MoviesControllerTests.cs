using Hive_Movie.Controllers;
using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.CurrentUser;
using Hive_Movie.Services.Movies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
namespace Tests.Integration.Controllers;

[Collection("Database collection")]
public class MoviesControllerTests(SqlServerFixture fixture) : IAsyncLifetime
{
    // Reset the database before every single test to guarantee a clean slate
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

    private MoviesController CreateController(ApplicationDbContext dbContext, string role)
    {
        // 1. Setup the real service connecting to the real Testcontainers DB
        var service = new MovieService(dbContext);
        var controller = new MoviesController(service);

        // 2. Fake the JWT Claims Principal
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // 3. Attach the fake user to the Controller's HttpContext
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };

        return controller;
    }

    // --- 1. GET ALL & GET BY ID (Public Read Endpoints) ---

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoMoviesExist()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "ROLE_USER");

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<MovieResponse>>(okResult.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetAll_ReturnsMovies_OrderedByReleaseDateDescending()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        dbContext.Movies.AddRange(
            new Movie
            {
                Id = Guid.NewGuid(),
                Title = "Old Movie",
                Description = "Desc",
                DurationMinutes = 120,
                ReleaseDate = new DateTime(2000, 1, 1)
            },
            new Movie
            {
                Id = Guid.NewGuid(),
                Title = "New Movie",
                Description = "Desc",
                DurationMinutes = 120,
                ReleaseDate = new DateTime(2025, 1, 1)
            }
        );
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "ROLE_USER");

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<MovieResponse>>(okResult.Value).ToList();

        Assert.Equal(2, list.Count);
        Assert.Equal("New Movie", list[0].Title); // Ensures temporal sorting is applied correctly!
    }

    [Fact]
    public async Task GetById_ExistingMovie_ReturnsOkWithData()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var movieId = Guid.NewGuid();
        dbContext.Movies.Add(new Movie
        {
            Id = movieId, Title = "The Matrix", Description = "Neo wakes up.", DurationMinutes = 136
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "ROLE_USER");

        // Act
        var result = await controller.GetById(movieId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<MovieResponse>(okResult.Value);
        Assert.Equal(movieId, response.Id);
        Assert.Equal("The Matrix", response.Title);
    }

    [Fact]
    public async Task GetById_NonExistentMovie_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "ROLE_USER");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.GetById(Guid.NewGuid()));
        Assert.Contains("not found", ex.Message);
    }

    // --- 2. CREATE (POST) ---

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreatedAtAction_AndSavesToDb()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "ROLE_ORGANIZER");

        var request = new CreateMovieRequest(
            "Inception",
            "Dream stealing.",
            148,
            new DateTime(2010, 7, 16),
            "https://poster.com/inception.jpg"
        );

        // Act
        var result = await controller.Create(request);

        // Assert HTTP Response
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var responseDto = Assert.IsType<MovieResponse>(createdResult.Value);

        Assert.Equal("Inception", responseDto.Title);
        Assert.Equal(nameof(controller.GetById), createdResult.ActionName);

        // Assert Database Integrity
        var savedMovie = await dbContext.Movies.FindAsync(responseDto.Id);
        Assert.NotNull(savedMovie);
        Assert.Equal(148, savedMovie.DurationMinutes);
    }

    // --- 3. UPDATE DETAILS (PUT) ---

    [Fact]
    public async Task Update_ValidRequest_UpdatesDetailsAndReturnsNoContent()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var movieId = Guid.NewGuid();
        dbContext.Movies.Add(new Movie
        {
            Id = movieId, Title = "Old Title", Description = "Old", DurationMinutes = 90
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "ROLE_ORGANIZER");
        var request = new UpdateMovieRequest("New Title", "New Desc", 120, DateTime.UtcNow, null);

        // Act
        var result = await controller.Update(movieId, request);

        // Assert
        Assert.IsType<NoContentResult>(result);

        var updatedMovie = await dbContext.Movies.FindAsync(movieId);
        Assert.Equal("New Title", updatedMovie!.Title);
        Assert.Equal(120, updatedMovie.DurationMinutes);
    }

    [Fact]
    public async Task Update_NonExistentMovie_ThrowsKeyNotFoundException()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "ROLE_SUPER_ADMIN");

        var request = new UpdateMovieRequest("N", "D", 100, DateTime.UtcNow, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.Update(Guid.NewGuid(), request));
    }

    // --- 4. DELETE (DELETE) ---

    [Fact]
    public async Task Delete_ExistingMovie_SoftDeletesAndReturnsNoContent()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var movieId = Guid.NewGuid();
        dbContext.Movies.Add(new Movie
        {
            Id = movieId, Title = "To Delete", Description = "D", DurationMinutes = 100
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "ROLE_ORGANIZER");

        // Act
        var result = await controller.Delete(movieId);

        // Assert
        Assert.IsType<NoContentResult>(result);

        // Prove Soft Delete triggered
        var deletedMovie = await dbContext.Movies.FindAsync(movieId);
        Assert.NotNull(deletedMovie);
        Assert.True(deletedMovie.IsDeleted);
        Assert.NotNull(deletedMovie.DeletedAtUtc);
    }

    [Fact]
    public async Task Delete_NonExistentMovie_ThrowsKeyNotFoundException()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "ROLE_ORGANIZER");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.Delete(Guid.NewGuid()));
    }

    // --- 5. SOFT-DELETE EDGE CASES (GHOST RECORDS) ---

    [Fact]
    public async Task Update_AlreadySoftDeletedMovie_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        var movie = new Movie
        {
            Id = Guid.NewGuid(), Title = "Ghost Movie", Description = "Spooky", DurationMinutes = 120
        };

        // 1. Add normally so interceptor doesn't fight us
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        // 2. Explicitly remove to trigger soft-delete interceptor logic
        dbContext.Movies.Remove(movie);
        await dbContext.SaveChangesAsync();

        // 3. Clear cache to force SQL query
        dbContext.ChangeTracker.Clear();

        var controller = CreateController(dbContext, "ROLE_ORGANIZER");
        var request = new UpdateMovieRequest("Trying to revive", "Desc", 120, DateTime.UtcNow, null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.Update(movie.Id, request));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task Delete_AlreadySoftDeletedMovie_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        var movie = new Movie
        {
            Id = Guid.NewGuid(), Title = "Ghost Movie", Description = "Spooky", DurationMinutes = 120
        };

        // 1. Add normally so interceptor doesn't fight us
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        // 2. Explicitly remove to trigger soft-delete interceptor logic
        dbContext.Movies.Remove(movie);
        await dbContext.SaveChangesAsync();

        // 3. Clear cache to force SQL query
        dbContext.ChangeTracker.Clear();

        var controller = CreateController(dbContext, "ROLE_ORGANIZER");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.Delete(movie.Id));
        Assert.Contains("not found", ex.Message);
    }
}