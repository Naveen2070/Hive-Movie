using Hive_Movie.Controllers;
using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.Cinemas;
using Hive_Movie.Services.CurrentUser;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
namespace Tests.Integration.Controllers;

[Collection("Database collection")]
public class CinemasControllerTests(SqlServerFixture fixture) : IAsyncLifetime
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

    private CinemasController CreateController(ApplicationDbContext dbContext, string? userId, string role)
    {
        // 1. Setup the real service connecting to the real Testcontainers DB
        var service = new CinemaService(dbContext);
        var controller = new CinemasController(service);

        // 2. Fake the JWT Claims Principal!
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role)
        };

        // Only add the ID claim if one was provided (helps test the missing ID edge case)
        if (userId != null)
        {
            claims.Add(new Claim("id", userId));
        }

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
    public async Task GetAll_ReturnsEmptyList_WhenNoCinemasExist()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Guest", "ROLE_USER");

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<CinemaResponse>>(okResult.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetAll_ReturnsCinemas_WhenDataExists()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        dbContext.Cinemas.AddRange(
            new Cinema
            {
                Id = Guid.NewGuid(),
                Name = "Cinema 1",
                Location = "Loc 1",
                OrganizerId = "Org1",
                ContactEmail = "1@test.com"
            },
            new Cinema
            {
                Id = Guid.NewGuid(),
                Name = "Cinema 2",
                Location = "Loc 2",
                OrganizerId = "Org2",
                ContactEmail = "2@test.com"
            }
        );
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "Guest", "ROLE_USER");

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<CinemaResponse>>(okResult.Value);
        Assert.Equal(2, list.Count());
    }

    [Fact]
    public async Task GetById_ExistingCinema_ReturnsOkWithData()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var cinemaId = Guid.NewGuid();
        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "IMAX Center",
            Location = "Downtown",
            OrganizerId = "Org1",
            ContactEmail = "test@test.com"
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "Guest", "ROLE_USER");

        // Act
        var result = await controller.GetById(cinemaId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CinemaResponse>(okResult.Value);
        Assert.Equal(cinemaId, response.Id);
        Assert.Equal("IMAX Center", response.Name);
    }

    [Fact]
    public async Task GetById_NonExistentCinema_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Guest", "ROLE_USER");

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
        var organizerId = "Org-Owner-123";
        var controller = CreateController(dbContext, organizerId, "ROLE_ORGANIZER");

        var request = new CreateCinemaRequest("New Cinema", "New Location", "contact@cinema.com");

        // Act
        var result = await controller.Create(request);

        // Assert HTTP Response
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var responseDto = Assert.IsType<CinemaResponse>(createdResult.Value);

        Assert.Equal("New Cinema", responseDto.Name);
        Assert.Equal(nameof(CinemaApprovalStatus.Pending), responseDto.ApprovalStatus); // Proves business logic applied

        // Assert Database Integrity
        var savedCinema = await dbContext.Cinemas.FindAsync(responseDto.Id);
        Assert.NotNull(savedCinema);
        Assert.Equal(organizerId, savedCinema.OrganizerId); // Proves JWT extraction worked
    }

    [Fact]
    public async Task Create_MissingUserIdClaim_ThrowsUnauthorizedAccess()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        // Pass NULL for the User ID to simulate a malformed JWT Token
        var controller = CreateController(dbContext, null, "ROLE_ORGANIZER");
        var request = new CreateCinemaRequest("Name", "Loc", "email@email.com");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Create(request));
        Assert.Equal("Missing User Id.", ex.Message);
    }

    // --- 3. UPDATE STATUS (PATCH) - SUPER ADMIN ONLY ---

    [Fact]
    public async Task UpdateStatus_ValidAdmin_UpdatesStatusAndReturnsNoContent()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var cinemaId = Guid.NewGuid();
        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "Test Cinema",
            Location = "Loc",
            OrganizerId = "Org",
            ContactEmail = "e@e.com",
            ApprovalStatus = CinemaApprovalStatus.Pending
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "SuperAdmin", "ROLE_SUPER_ADMIN");

        // Act
        var result = await controller.UpdateStatus(cinemaId, CinemaApprovalStatus.Approved);

        // Assert
        Assert.IsType<NoContentResult>(result);

        var updatedCinema = await dbContext.Cinemas.FindAsync(cinemaId);
        Assert.Equal(CinemaApprovalStatus.Approved, updatedCinema!.ApprovalStatus);
    }

    [Fact]
    public async Task UpdateStatus_NonExistentCinema_ThrowsKeyNotFoundException()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "SuperAdmin", "ROLE_SUPER_ADMIN");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.UpdateStatus(Guid.NewGuid(), CinemaApprovalStatus.Approved));
    }

    // --- 4. UPDATE DETAILS (PUT) ---

    [Fact]
    public async Task Update_ValidRequest_UpdatesDetailsAndReturnsNoContent()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var cinemaId = Guid.NewGuid();
        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "Old Name",
            Location = "Old Loc",
            OrganizerId = "Org",
            ContactEmail = "e@e.com"
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "Org", "ROLE_ORGANIZER");
        var request = new UpdateCinemaRequest("New Name", "New Loc");

        // Act
        var result = await controller.Update(cinemaId, request);

        // Assert
        Assert.IsType<NoContentResult>(result);

        var updatedCinema = await dbContext.Cinemas.FindAsync(cinemaId);
        Assert.Equal("New Name", updatedCinema!.Name);
        Assert.Equal("New Loc", updatedCinema.Location);
    }

    [Fact]
    public async Task Update_NonExistentCinema_ThrowsKeyNotFoundException()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Org", "ROLE_ORGANIZER");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.Update(Guid.NewGuid(), new UpdateCinemaRequest("N", "L")));
    }

    // --- 5. DELETE (DELETE) ---

    [Fact]
    public async Task Delete_ExistingCinema_SoftDeletesAndReturnsNoContent()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var cinemaId = Guid.NewGuid();
        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "To Delete",
            Location = "Loc",
            OrganizerId = "Org",
            ContactEmail = "e@e.com"
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "Org", "ROLE_ORGANIZER");

        // Act
        var result = await controller.Delete(cinemaId);

        // Assert
        Assert.IsType<NoContentResult>(result);

        var deletedCinema = await dbContext.Cinemas.FindAsync(cinemaId);
        Assert.NotNull(deletedCinema);
        Assert.True(deletedCinema.IsDeleted); // Proves Soft Delete triggered
    }

    [Fact]
    public async Task Delete_NonExistentCinema_ThrowsKeyNotFoundException()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Org", "ROLE_ORGANIZER");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.Delete(Guid.NewGuid()));
    }

    // --- 6. RBAC SECURITY EDGE CASES (IDOR PREVENTION) ---

    [Fact]
    public async Task Update_WrongOrganizer_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var realOwnerId = "Org-RealOwner";
        var cinemaId = Guid.NewGuid();

        // RealOwner creates a cinema
        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "Safe Cinema",
            Location = "Loc",
            OrganizerId = realOwnerId,
            ContactEmail = "e@e.com"
        });
        await dbContext.SaveChangesAsync();

        // The Hacker is logged in as an Organizer, but they DO NOT own this cinema
        var controller = CreateController(dbContext, "Hacker-Org", "ROLE_ORGANIZER");
        var request = new UpdateCinemaRequest("Hacked Name", "Hacked Loc");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Update(cinemaId, request));
        Assert.Equal("You are not authorized to update this cinema.", ex.Message);
    }

    [Fact]
    public async Task Delete_WrongOrganizer_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var realOwnerId = "Org-RealOwner";
        var cinemaId = Guid.NewGuid();

        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "Safe Cinema",
            Location = "Loc",
            OrganizerId = realOwnerId,
            ContactEmail = "e@e.com"
        });
        await dbContext.SaveChangesAsync();

        // Hacker attempts to delete it
        var controller = CreateController(dbContext, "Hacker-Org", "ROLE_ORGANIZER");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Delete(cinemaId));
        Assert.Equal("You are not authorized to delete this cinema.", ex.Message);
    }

    [Fact]
    public async Task Delete_SuperAdmin_OverridesOwnershipCheck_AndReturnsNoContent()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var realOwnerId = "Org-RealOwner";
        var cinemaId = Guid.NewGuid();

        dbContext.Cinemas.Add(new Cinema
        {
            Id = cinemaId,
            Name = "Admin Target",
            Location = "Loc",
            OrganizerId = realOwnerId,
            ContactEmail = "e@e.com"
        });
        await dbContext.SaveChangesAsync();

        // Admin doesn't own it, but has the SUPER_ADMIN role
        var controller = CreateController(dbContext, "SuperAdminUser", "ROLE_SUPER_ADMIN");

        // Act
        var result = await controller.Delete(cinemaId);

        // Assert
        Assert.IsType<NoContentResult>(result);

        var deleted = await dbContext.Cinemas.FindAsync(cinemaId);
        Assert.True(deleted!.IsDeleted);
    }
}