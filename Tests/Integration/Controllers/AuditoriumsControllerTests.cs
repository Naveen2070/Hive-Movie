using FluentValidation;
using Hive_Movie.Controllers;
using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.Auditoriums;
using Hive_Movie.Services.CurrentUser;
using Hive_Movie.Validators.Auditorium;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
namespace Tests.Integration.Controllers;

[Collection("Database collection")]
public class AuditoriumsControllerTests(SqlServerFixture fixture) : IAsyncLifetime
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

        // The service layer doesn't need the user info for Auditoriums, the Controller handles it
        var mockUser = new Mock<ICurrentUserService>();
        return new ApplicationDbContext(options, mockUser.Object);
    }

    private AuditoriumsController CreateController(ApplicationDbContext dbContext, string userId, string role)
    {
        // 1. Setup the real validators and service
        var createValidator = new CreateAuditoriumRequestValidator();
        var updateValidator = new UpdateAuditoriumRequestValidator();
        var service = new AuditoriumService(dbContext, createValidator, updateValidator);

        var controller = new AuditoriumsController(service);

        // 2. Fake the JWT Claims Principal!
        var claims = new List<Claim>
        {
            new("id", userId), // Matches: User.FindFirst("id")
            new(ClaimTypes.Role, role) // Matches: User.IsInRole(...)
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

    private async Task<Cinema> SeedCinemaAsync(ApplicationDbContext dbContext, string organizerId)
    {
        var cinema = new Cinema
        {
            Id = Guid.NewGuid(),
            Name = "Integration Cinema",
            ContactEmail = "test@hive.com",
            Location = "Test",
            OrganizerId = organizerId
        };
        dbContext.Cinemas.Add(cinema);
        await dbContext.SaveChangesAsync();
        return cinema;
    }

    // --- INTEGRATION TESTS ---

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreatedAtAction_AndSavesToDb()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var organizerId = "Org-123";
        var cinema = await SeedCinemaAsync(dbContext, organizerId);

        // Ensure the controller thinks "Org-123" is logged in with the Organizer role
        var controller = CreateController(dbContext, organizerId, "ROLE_ORGANIZER");

        var layoutDto = new AuditoriumLayoutDto([], [], []);
        var request = new CreateAuditoriumRequest(cinema.Id, "IMAX Screen", 10, 10, layoutDto);

        // Act
        var result = await controller.Create(request);

        // Assert HTTP Response
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var responseDto = Assert.IsType<AuditoriumResponse>(createdResult.Value);

        Assert.Equal("IMAX Screen", responseDto.Name);
        Assert.Equal(nameof(controller.GetById), createdResult.ActionName);

        // Assert Database Integrity
        var savedAuditorium = await dbContext.Auditoriums.FindAsync(responseDto.Id);
        Assert.NotNull(savedAuditorium);
    }

    [Fact]
    public async Task Create_ValidationFails_ThrowsFluentValidationException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Org-123", "ROLE_ORGANIZER");

        // Invalid Data: MaxRows is 0!
        var layoutDto = new AuditoriumLayoutDto([], [], []);
        var request = new CreateAuditoriumRequest(Guid.NewGuid(), "Bad Room", 0, 10, layoutDto);

        // Act & Assert
        // We expect FluentValidation to intercept this and throw before EF Core runs.
        // (Note: In a fully hosted API, your GlobalExceptionHandler converts this to a 400 Bad Request)
        await Assert.ThrowsAsync<ValidationException>(() => controller.Create(request));
    }

    [Fact]
    public async Task GetById_ExistingAuditorium_ReturnsOkWithData()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var cinema = await SeedCinemaAsync(dbContext, "Org-1");
        var auditoriumId = Guid.NewGuid();

        dbContext.Auditoriums.Add(new Auditorium
        {
            Id = auditoriumId,
            CinemaId = cinema.Id,
            Name = "Screen 1",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        });
        await dbContext.SaveChangesAsync();

        // GetById has [AllowAnonymous], so the user roles don't matter here
        var controller = CreateController(dbContext, "Guest", "ROLE_USER");

        // Act
        var result = await controller.GetById(auditoriumId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseDto = Assert.IsType<AuditoriumResponse>(okResult.Value);
        Assert.Equal(auditoriumId, responseDto.Id);
        Assert.Equal("Screen 1", responseDto.Name);
    }

    [Fact]
    public async Task Delete_ValidOrganizer_ReturnsNoContent_AndSoftDeletes()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var organizerId = "Org-123";
        var cinema = await SeedCinemaAsync(dbContext, organizerId);
        var auditoriumId = Guid.NewGuid();

        dbContext.Auditoriums.Add(new Auditorium
        {
            Id = auditoriumId,
            CinemaId = cinema.Id,
            Name = "Doomed Screen",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, organizerId, "ROLE_ORGANIZER");

        // Act
        var result = await controller.Delete(auditoriumId);

        // Assert HTTP Response
        Assert.IsType<NoContentResult>(result);

        // Assert DB state (Soft Deleted)
        var softDeleted = await dbContext.Auditoriums.FindAsync(auditoriumId);
        Assert.NotNull(softDeleted);
        Assert.True(softDeleted.IsDeleted);
    }

    [Fact]
    public async Task Delete_WrongOrganizer_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var realOwnerId = "Org-RealOwner";
        var cinema = await SeedCinemaAsync(dbContext, realOwnerId);

        var auditoriumId = Guid.NewGuid();
        dbContext.Auditoriums.Add(new Auditorium
        {
            Id = auditoriumId,
            CinemaId = cinema.Id,
            Name = "Target Screen",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        });
        await dbContext.SaveChangesAsync();

        // The hacker is logged in as an Organizer, but they DO NOT own this cinema
        var controller = CreateController(dbContext, "Hacker-Org", "ROLE_ORGANIZER");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Delete(auditoriumId));
        Assert.Equal("You do not own this cinema.", ex.Message);
    }

    [Fact]
    public async Task Update_SuperAdmin_OverridesOwnershipCheck_AndReturnsNoContent()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var realOwnerId = "Org-RealOwner";
        var cinema = await SeedCinemaAsync(dbContext, realOwnerId);

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

        // The user is an Admin, so they should bypass the ownership check
        var controller = CreateController(dbContext, "SuperAdminUser", "ROLE_SUPER_ADMIN");
        var request = new UpdateAuditoriumRequest("Admin Updated Name", 15, 15, new AuditoriumLayoutDto([], [], []));

        // Act
        var result = await controller.Update(auditoriumId, request);

        // Assert
        Assert.IsType<NoContentResult>(result);

        var updated = await dbContext.Auditoriums.FindAsync(auditoriumId);
        Assert.Equal("Admin Updated Name", updated!.Name);
    }

    [Fact]
    public async Task GetById_NonExistentId_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Guest", "ROLE_USER");
        var fakeId = Guid.NewGuid();

        // Act & Assert
        // The GlobalExceptionHandler will catch this and return a 404 ProblemDetails to the client
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.GetById(fakeId));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task Create_NonExistentCinemaId_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Org-1", "ROLE_ORGANIZER");

        // CinemaId does not exist in the DB
        var request = new CreateAuditoriumRequest(Guid.NewGuid(), "Screen", 10, 10, new AuditoriumLayoutDto([], [], []));

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.Create(request));
    }

    [Fact]
    public async Task GetAll_ReturnsOkResult_WithList()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
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

        var controller = CreateController(dbContext, "Guest", "ROLE_USER");

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<AuditoriumResponse>>(okResult.Value);
        Assert.Equal(2, list.Count());
    }

    [Fact]
    public async Task GetByCinemaId_ReturnsOnlyAuditoriumsForThatCinema()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var targetCinema = await SeedCinemaAsync(dbContext, "Org-1");
        var otherCinema = await SeedCinemaAsync(dbContext, "Org-2");

        dbContext.Auditoriums.Add(new Auditorium
        {
            Id = Guid.NewGuid(),
            CinemaId = targetCinema.Id,
            Name = "Target Screen",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        });
        dbContext.Auditoriums.Add(new Auditorium
        {
            Id = Guid.NewGuid(),
            CinemaId = otherCinema.Id,
            Name = "Other Screen",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout()
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "Guest", "ROLE_USER");

        // Act
        var result = await controller.GetByCinemaId(targetCinema.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<AuditoriumResponse>>(okResult.Value).ToList();

        Assert.Single(list);
        Assert.Equal("Target Screen", list[0].Name);
    }
}