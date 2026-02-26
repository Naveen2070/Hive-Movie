using Hive_Movie.Controllers;
using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.CurrentUser;
using Hive_Movie.Services.Tickets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System.Security.Claims;
namespace Tests.Integration.Controllers;

[Collection("Database collection")]
public class TicketsControllerTests(SqlServerFixture fixture) : IAsyncLifetime
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

    private TicketsController CreateController(ApplicationDbContext dbContext, string? userId)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new TicketService(dbContext, cache);
        var controller = new TicketsController(service);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "ROLE_USER")
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

    // Helper to seed a valid Showtime hierarchy
    private async Task<Showtime> SeedShowtimeAsync(ApplicationDbContext dbContext)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(), Title = "The Matrix", Description = "A movie about a man who is a computer programmer", DurationMinutes = 136
        };
        var cinema = new Cinema
        {
            Id = Guid.NewGuid(),
            Name = "Hive Cinema",
            ContactEmail = "contact@hivecinema.com",
            Location = "Downtown",
            OrganizerId = "Org-1"
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
            BasePrice = 10.00m,
            SeatAvailabilityState = new byte[100] // 100 available seats
        };

        dbContext.Movies.Add(movie);
        dbContext.Cinemas.Add(cinema);
        dbContext.Auditoriums.Add(auditorium);
        dbContext.Showtimes.Add(showtime);
        await dbContext.SaveChangesAsync();

        return showtime;
    }

    // --- 1. SEAT RESERVATION (POST) ---

    [Fact]
    public async Task ReserveTickets_ValidRequest_CreatesPendingTicket_AndReturnsCreated()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var showtime = await SeedShowtimeAsync(dbContext);

        var controller = CreateController(dbContext, "User-123");
        var request = new ReserveTicketRequest(showtime.Id, [new SeatCoordinateDto(0, 0)]);

        // Act
        var result = await controller.ReserveTickets(request);

        // Assert HTTP Response
        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<TicketCheckoutResponse>(createdResult.Value);

        Assert.StartsWith("HIVE-", response.BookingReference);
        Assert.Equal(10.00m, response.TotalAmount);
        Assert.Equal(nameof(TicketStatus.Pending), response.Status);

        // Assert Database Integrity & State Mutation
        var ticket = await dbContext.Tickets.FindAsync(response.TicketId);
        Assert.NotNull(ticket);
        Assert.Equal("User-123", ticket.UserId);

        var updatedShowtime = await dbContext.Showtimes.FindAsync(showtime.Id);
        Assert.Equal(1, updatedShowtime!.SeatAvailabilityState[0]); // Seat 0,0 is now Reserved (1)
    }

    [Fact]
    public async Task ReserveTickets_SeatAlreadyTaken_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var showtime = await SeedShowtimeAsync(dbContext);

        // Simulating that someone already bought Seat (0,0)
        showtime.SeatAvailabilityState[0] = 2; // 2 = Sold
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "User-123");
        var request = new ReserveTicketRequest(showtime.Id, [new SeatCoordinateDto(0, 0)]);

        // Act & Assert
        // The Global Error Handler maps InvalidOperationException to 409 Conflict
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.ReserveTickets(request));
        Assert.Contains("no longer available", ex.Message);
    }

    [Fact]
    public async Task ReserveTickets_MissingUserIdClaim_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, null); // Missing "id" claim in JWT

        var request = new ReserveTicketRequest(Guid.NewGuid(), [new SeatCoordinateDto(0, 0)]);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.ReserveTickets(request));
    }

    // --- 2. GET MY BOOKINGS (GET) ---

    [Fact]
    public async Task GetMyTickets_ReturnsOnlyTicketsForAuthenticatedUser()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var showtime = await SeedShowtimeAsync(dbContext);

        // Seed 2 tickets for User-A, and 1 for User-B
        dbContext.Tickets.AddRange(
            new Ticket
            {
                Id = Guid.NewGuid(),
                UserId = "User-A",
                ShowtimeId = showtime.Id,
                BookingReference = "HIVE-A1",
                TotalAmount = 10,
                Status = TicketStatus.Confirmed
            },
            new Ticket
            {
                Id = Guid.NewGuid(),
                UserId = "User-A",
                ShowtimeId = showtime.Id,
                BookingReference = "HIVE-A2",
                TotalAmount = 10,
                Status = TicketStatus.Pending
            },
            new Ticket
            {
                Id = Guid.NewGuid(),
                UserId = "User-B",
                ShowtimeId = showtime.Id,
                BookingReference = "HIVE-B1",
                TotalAmount = 10,
                Status = TicketStatus.Confirmed
            }
        );
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, "User-A");

        // Act
        var result = await controller.GetMyTickets();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var tickets = Assert.IsAssignableFrom<IEnumerable<MyTicketResponse>>(okResult.Value).ToList();

        // Proves data isolation: User-A should only see their 2 tickets
        Assert.Equal(2, tickets.Count);
        Assert.DoesNotContain(tickets, t => t.BookingReference == "HIVE-B1");
    }

    // --- 3. PAYMENT WEBHOOKS (POST) ---

    [Fact]
    public async Task PaymentSuccessWebhook_ValidPendingTicket_ConfirmsTicketAndSellsSeats()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var showtime = await SeedShowtimeAsync(dbContext);

        // Seat is currently "Reserved" (1)
        showtime.SeatAvailabilityState[0] = 1;

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            UserId = "User-123",
            ShowtimeId = showtime.Id,
            BookingReference = "HIVE-WEBHOOK",
            TotalAmount = 10,
            Status = TicketStatus.Pending,
            ReservedSeats =
            [
                new SeatCoordinate
                {
                    Row = 0, Col = 0
                }
            ]
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, null); // Webhooks are [AllowAnonymous]

        // FIX: Using record primary constructor syntax
        var payload = new PaymentWebhookPayload("HIVE-WEBHOOK", "TX-123", "SUCCESS");

        // Act
        var result = await controller.PaymentSuccessWebhook(payload);

        // Assert HTTP Response
        Assert.IsType<OkResult>(result);

        // Assert DB state mutations
        var updatedTicket = await dbContext.Tickets.FindAsync(ticket.Id);
        Assert.Equal(TicketStatus.Confirmed, updatedTicket!.Status);
        Assert.NotNull(updatedTicket.PaidAtUtc);

        var updatedShowtime = await dbContext.Showtimes.FindAsync(showtime.Id);
        Assert.Equal(2, updatedShowtime!.SeatAvailabilityState[0]); // Seat is now "Sold" (2)
    }

    [Fact]
    public async Task PaymentSuccessWebhook_NonExistentBooking_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, null);

        // FIX: Using record primary constructor syntax
        var payload = new PaymentWebhookPayload("INVALID-REF", "TX-123", "FAILED");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.PaymentSuccessWebhook(payload));
        Assert.Contains("No ticket found with reference", ex.Message);
    }

    [Fact]
    public async Task PaymentSuccessWebhook_ExpiredTicket_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var showtime = await SeedShowtimeAsync(dbContext);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            UserId = "User-123",
            ShowtimeId = showtime.Id,
            BookingReference = "HIVE-LATE",
            TotalAmount = 10,
            Status = TicketStatus.Expired, // Worker already killed it!
            ReservedSeats = []
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, null);

        // FIX: Using record primary constructor syntax
        var payload = new PaymentWebhookPayload("HIVE-LATE", "TX-123", "EXPIRED");

        // Act & Assert
        // If the background cleanup worker expired the ticket, the webhook must fail so Stripe issues a refund
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.PaymentSuccessWebhook(payload));
        Assert.Contains("cannot be confirmed", ex.Message);
    }

    [Fact]
    public async Task PaymentSuccessWebhook_AlreadyConfirmedTicket_IsIdempotentAndReturnsOk()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var showtime = await SeedShowtimeAsync(dbContext);

        // Simulating a ticket that was ALREADY successfully processed 5 minutes ago
        showtime.SeatAvailabilityState[0] = 2; // Seat is Sold

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            UserId = "User-123",
            ShowtimeId = showtime.Id,
            BookingReference = "HIVE-DOUBLE",
            TotalAmount = 10,
            Status = TicketStatus.Confirmed, // Already Confirmed!
            PaidAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ReservedSeats =
            [
                new SeatCoordinate
                {
                    Row = 0, Col = 0
                }
            ]
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, null);

        // FIX: Using record primary constructor syntax
        var payload = new PaymentWebhookPayload("HIVE-DOUBLE", "TX-123", "CONFIRMED");

        // Act
        // Stripe fires the exact same webhook a second time...
        var result = await controller.PaymentSuccessWebhook(payload);

        // Assert
        // The API MUST return 200 OK silently so Stripe stops retrying. 
        // It must NOT throw an InvalidOperationException.
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ReserveTickets_NonExistentShowtime_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, "User-123");

        // Providing a completely made-up Showtime ID
        var request = new ReserveTicketRequest(Guid.NewGuid(), [new SeatCoordinateDto(0, 0)]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => controller.ReserveTickets(request));
        Assert.Contains("Showtime not found", ex.Message);
    }

    [Fact]
    public async Task ReserveTickets_OutOfBoundsSeat_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var showtime = await SeedShowtimeAsync(dbContext); // Grid is 10x10

        var controller = CreateController(dbContext, "User-123");

        // User tries to hack the system by requesting Row 99
        var request = new ReserveTicketRequest(showtime.Id, [new SeatCoordinateDto(99, 99)]);

        // Act & Assert
        // The Engine bounds-checking intercepts this before it corrupts the byte array!
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => controller.ReserveTickets(request));
    }

    [Fact]
    public async Task GetMyTickets_NoTickets_ReturnsEmptyList()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        // We seed a showtime, but ZERO tickets for this user
        await SeedShowtimeAsync(dbContext);

        var controller = CreateController(dbContext, "BrandNewUser");

        // Act
        var result = await controller.GetMyTickets();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var tickets = Assert.IsAssignableFrom<IEnumerable<MyTicketResponse>>(okResult.Value);

        // Should gracefully return an empty array [], not null, and not crash
        Assert.Empty(tickets);
    }
}