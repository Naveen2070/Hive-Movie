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
    public Task InitializeAsync()
    {
        return fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

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
        var service = new CinemaService(dbContext);
        var controller = new CinemasController(service);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role)
        };

        if (userId != null)
        {
            claims.Add(new Claim("id", userId));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };

        return controller;
    }

    private async static Task<Cinema> SeedCinemaAsync(
        ApplicationDbContext dbContext,
        string organizerId = "Org1",
        string name = "Test Cinema",
        CinemaApprovalStatus status = CinemaApprovalStatus.Approved)
    {
        var cinema = new Cinema
        {
            Id = Guid.NewGuid(),
            Name = name,
            Location = "Test Location",
            OrganizerId = organizerId,
            ContactEmail = $"{organizerId}@test.com",
            ApprovalStatus = status
        };
        dbContext.Cinemas.Add(cinema);
        await dbContext.SaveChangesAsync();
        return cinema;
    }

    // --- 1. GET ALL & GET BY ID (Public & Organizer Read Endpoints) ---

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoCinemasExist()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Guest", "USER");

        var result = await controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PagedResponse<CinemaResponse>>(okResult.Value);
        Assert.Empty(response.Content);
    }

    [Fact]
    public async Task GetAll_ReturnsCinemas_WhenDataExists()
    {
        await using var dbContext = CreateDbContext();
        await SeedCinemaAsync(dbContext, "Org1", "Cinema 1");
        await SeedCinemaAsync(dbContext, "Org2", "Cinema 2");

        var controller = CreateController(dbContext, "Guest", "USER");

        var result = await controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PagedResponse<CinemaResponse>>(okResult.Value);
        Assert.Equal(2, response.Content.Count());
    }

    [Fact]
    public async Task GetAllByOrganizer_ReturnsOnlyCinemasForLoggedInOrganizer()
    {
        await using var dbContext = CreateDbContext();
        await SeedCinemaAsync(dbContext, "Org1", "Org1 Cinema");
        await SeedCinemaAsync(dbContext, "Org2", "Org2 Cinema");

        var controller = CreateController(dbContext, "Org1", "ORGANIZER");

        var result = await controller.GetAllByOrganizer();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PagedResponse<CinemaResponse>>(okResult.Value);
        var list = response.Content.ToList();
        Assert.Single(list);
        Assert.Equal("Org1 Cinema", list.First().Name);
    }

    [Fact]
    public async Task GetById_ExistingCinema_ReturnsOkWithData()
    {
        await using var dbContext = CreateDbContext();
        var cinema = await SeedCinemaAsync(dbContext, "Org1", "IMAX Center");

        var controller = CreateController(dbContext, "Guest", "USER");

        var result = await controller.GetById(cinema.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CinemaResponse>(okResult.Value);
        Assert.Equal(cinema.Id, response.Id);
        Assert.Equal("IMAX Center", response.Name);
    }

    [Fact]
    public async Task GetById_NonExistentCinema_ThrowsKeyNotFoundException()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Guest", "USER");

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.GetById(Guid.NewGuid()));
        Assert.Contains("not found", ex.Message);
    }

    // --- 2. CREATE (POST) ---

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreatedAtAction_AndSavesToDb_WithOutboxMessage()
    {
        await using var dbContext = CreateDbContext();
        var organizerId = "Org-Owner-123";
        var controller = CreateController(dbContext, organizerId, "ORGANIZER");

        var request = new CreateCinemaRequest("New Cinema", "New Location", "contact@cinema.com");

        var result = await controller.Create(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var responseDto = Assert.IsType<CinemaResponse>(createdResult.Value);

        Assert.Equal("New Cinema", responseDto.Name);
        Assert.Equal(nameof(CinemaApprovalStatus.Pending), responseDto.ApprovalStatus);

        var savedCinema = await dbContext.Cinemas.FindAsync(responseDto.Id);
        Assert.NotNull(savedCinema);
        Assert.Equal(organizerId, savedCinema.OrganizerId);

        // Assert Outbox Message was created for the Email
        var outboxMessage = await dbContext.OutboxMessages.FirstOrDefaultAsync();
        Assert.NotNull(outboxMessage);
        Assert.Equal("EmailNotification", outboxMessage.EventType);
        Assert.Contains("CINEMA_PENDING_APPROVAL", outboxMessage.Payload);
    }

    [Fact]
    public async Task Create_MissingUserIdClaim_ThrowsUnauthorizedAccess()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, null, "ORGANIZER");
        var request = new CreateCinemaRequest("Name", "Loc", "email@email.com");

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Create(request));
        Assert.Equal("Missing User Id.", ex.Message);
    }

    // --- 3. UPDATE STATUS (PATCH) - SUPER ADMIN ONLY ---

    [Fact]
    public async Task UpdateStatus_ValidAdmin_UpdatesStatus_AndCreatesOutboxMessage()
    {
        await using var dbContext = CreateDbContext();
        var cinema = await SeedCinemaAsync(dbContext, "Org", "Test Cinema", CinemaApprovalStatus.Pending);

        var controller = CreateController(dbContext, "SuperAdmin", "SUPER_ADMIN");

        var result = await controller.UpdateStatus(cinema.Id, CinemaApprovalStatus.Approved);

        Assert.IsType<NoContentResult>(result);

        var updatedCinema = await dbContext.Cinemas.FindAsync(cinema.Id);
        Assert.Equal(CinemaApprovalStatus.Approved, updatedCinema!.ApprovalStatus);

        // Assert Outbox Message was created for the Email
        var outboxMessage = await dbContext.OutboxMessages.FirstOrDefaultAsync();
        Assert.NotNull(outboxMessage);
        Assert.Equal("EmailNotification", outboxMessage.EventType);
        Assert.Contains("CINEMA_STATUS_UPDATE", outboxMessage.Payload);
    }

    [Fact]
    public async Task UpdateStatus_NonExistentCinema_ThrowsKeyNotFoundException()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "SuperAdmin", "SUPER_ADMIN");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.UpdateStatus(Guid.NewGuid(), CinemaApprovalStatus.Approved));
    }

    // --- 4. UPDATE DETAILS (PUT) ---

    [Fact]
    public async Task Update_ValidRequest_UpdatesDetailsAndReturnsNoContent()
    {
        await using var dbContext = CreateDbContext();
        var cinema = await SeedCinemaAsync(dbContext, "Org", "Old Name");

        var controller = CreateController(dbContext, "Org", "ORGANIZER");
        var request = new UpdateCinemaRequest("New Name", "New Loc");

        var result = await controller.Update(cinema.Id, request);

        Assert.IsType<NoContentResult>(result);

        var updatedCinema = await dbContext.Cinemas.FindAsync(cinema.Id);
        Assert.Equal("New Name", updatedCinema!.Name);
        Assert.Equal("New Loc", updatedCinema.Location);
    }

    [Fact]
    public async Task Update_NonExistentCinema_ThrowsKeyNotFoundException()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Org", "ORGANIZER");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            controller.Update(Guid.NewGuid(), new UpdateCinemaRequest("N", "L")));
    }

    // --- 5. DELETE (DELETE) ---

    [Fact]
    public async Task Delete_ExistingCinema_SoftDeletesAndReturnsNoContent()
    {
        await using var dbContext = CreateDbContext();
        var cinema = await SeedCinemaAsync(dbContext, "Org", "To Delete");

        var controller = CreateController(dbContext, "Org", "ORGANIZER");

        var result = await controller.Delete(cinema.Id);

        Assert.IsType<NoContentResult>(result);

        var deletedCinema = await dbContext.Cinemas.FindAsync(cinema.Id);
        Assert.NotNull(deletedCinema);
        Assert.True(deletedCinema.IsDeleted);
    }

    [Fact]
    public async Task Delete_NonExistentCinema_ThrowsKeyNotFoundException()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "Org", "ORGANIZER");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.Delete(Guid.NewGuid()));
    }

    // --- 6. RBAC SECURITY EDGE CASES (IDOR PREVENTION) ---

    [Fact]
    public async Task Update_WrongOrganizer_ThrowsUnauthorizedAccessException()
    {
        await using var dbContext = CreateDbContext();
        var realOwnerId = "Org-RealOwner";
        var cinema = await SeedCinemaAsync(dbContext, realOwnerId, "Safe Cinema");

        var controller = CreateController(dbContext, "Hacker-Org", "ORGANIZER");
        var request = new UpdateCinemaRequest("Hacked Name", "Hacked Loc");

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Update(cinema.Id, request));
        Assert.Equal("You are not authorized to update this cinema.", ex.Message);
    }

    [Fact]
    public async Task Delete_WrongOrganizer_ThrowsUnauthorizedAccessException()
    {
        await using var dbContext = CreateDbContext();
        var realOwnerId = "Org-RealOwner";
        var cinema = await SeedCinemaAsync(dbContext, realOwnerId, "Safe Cinema");

        var controller = CreateController(dbContext, "Hacker-Org", "ORGANIZER");

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Delete(cinema.Id));
        Assert.Equal("You are not authorized to delete this cinema.", ex.Message);
    }

    [Fact]
    public async Task Delete_SuperAdmin_OverridesOwnershipCheck_AndReturnsNoContent()
    {
        await using var dbContext = CreateDbContext();
        var realOwnerId = "Org-RealOwner";
        var cinema = await SeedCinemaAsync(dbContext, realOwnerId, "Admin Target");

        var controller = CreateController(dbContext, "SuperAdminUser", "SUPER_ADMIN");

        var result = await controller.Delete(cinema.Id);

        Assert.IsType<NoContentResult>(result);

        var deleted = await dbContext.Cinemas.FindAsync(cinema.Id);
        Assert.True(deleted!.IsDeleted);
    }
}