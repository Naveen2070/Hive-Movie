using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.CurrentUser;
using Hive_Movie.Services.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
namespace Tests.Services;

public class TicketServiceTests
{
    // Helper: Creates a pristine In-Memory database and a real MemoryCache for each test
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

    // Helper: Seeds a valid movie, cinema, auditorium, and showtime
    private async static Task<Showtime> SeedStandardShowtimeAsync(ApplicationDbContext dbContext)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(), Title = "The Matrix", Description = "Neo wakes up.", DurationMinutes = 120
        };
        var cinema = new Cinema
        {
            Id = Guid.NewGuid(),
            ContactEmail = "test@hive.com",
            Name = "Hive Downtown",
            Location = "City Center",
            OrganizerId = "Org-1"
        };

        var auditorium = new Auditorium
        {
            Id = Guid.NewGuid(),
            CinemaId = cinema.Id,
            Name = "Screen 1",
            MaxRows = 10,
            MaxColumns = 10,
            LayoutConfiguration = new AuditoriumLayout
            {
                Tiers = new List<SeatTier>
                {
                    new()
                    {
                        TierName = "VIP",
                        PriceSurcharge = 5.00m,
                        Seats = new List<SeatCoordinate>
                        {
                            new()
                            {
                                Row = 5, Col = 5
                            }
                        }
                    }
                }
            }
        };

        var showtime = new Showtime
        {
            Id = Guid.NewGuid(),
            MovieId = movie.Id,
            AuditoriumId = auditorium.Id,
            StartTimeUtc = DateTime.UtcNow.AddDays(1),
            BasePrice = 10.00m,
            SeatAvailabilityState = new byte[100] // 100 empty seats (Available = 0)
        };

        dbContext.Movies.Add(movie);
        dbContext.Cinemas.Add(cinema);
        dbContext.Auditoriums.Add(auditorium);
        dbContext.Showtimes.Add(showtime);
        await dbContext.SaveChangesAsync();

        return showtime;
    }

    // --- 1. TESTS FOR: ReserveTicketsAsync ---

    [Fact]
    public async Task ReserveTicketsAsync_HappyPath_CalculatesPriceAndLocksSeats()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new TicketService(dbContext, cache);
        var showtime = await SeedStandardShowtimeAsync(dbContext);

        var request = new ReserveTicketRequest(showtime.Id, [
            new SeatCoordinateDto(0, 0), // Standard: $10
            new SeatCoordinateDto(5, 5)
        ]);

        // Act
        var result = await service.ReserveTicketsAsync(request, "UserA");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(nameof(TicketStatus.Pending), result.Status);
        Assert.Equal(25.00m, result.TotalAmount); // 10 + 15 = 25
        Assert.StartsWith("HIVE-", result.BookingReference);

        var savedTicket = await dbContext.Tickets.FirstAsync(t => t.Id == result.TicketId);
        Assert.Equal("UserA", savedTicket.UserId);
        Assert.Equal(2, savedTicket.ReservedSeats.Count);

        // Verify SeatMapEngine mathematically changed the bytes to 1 (Reserved)
        var updatedShowtime = await dbContext.Showtimes.AsNoTracking().FirstAsync(s => s.Id == showtime.Id);
        Assert.Equal(1, updatedShowtime.SeatAvailabilityState[0]); // (0 * 10) + 0
        Assert.Equal(1, updatedShowtime.SeatAvailabilityState[55]); // (5 * 10) + 5
    }

    [Fact]
    public async Task ReserveTicketsAsync_MissingShowtime_ThrowsKeyNotFound()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new TicketService(dbContext, cache);
        var request = new ReserveTicketRequest(Guid.NewGuid(), [new SeatCoordinateDto(0, 0)]);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReserveTicketsAsync(request, "UserA"));
    }

    [Fact]
    public async Task ReserveTicketsAsync_SeatAlreadyTaken_ThrowsInvalidOperation()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new TicketService(dbContext, cache);
        var showtime = await SeedStandardShowtimeAsync(dbContext);

        // Manually steal Row 0, Col 0 by setting it to Reserved (1)
        showtime.SeatAvailabilityState[0] = 1;
        await dbContext.SaveChangesAsync();

        var request = new ReserveTicketRequest(showtime.Id, [new SeatCoordinateDto(0, 0)]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReserveTicketsAsync(request, "UserA"));
        Assert.Equal("One or more selected seats are no longer available.", ex.Message);
    }

    // --- 2. TESTS FOR: GetMyTicketsAsync ---

    [Fact]
    public async Task GetMyTicketsAsync_ReturnsUserTickets_InDescendingOrder()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new TicketService(dbContext, cache);
        var showtime = await SeedStandardShowtimeAsync(dbContext);

        var olderTicket = new Ticket
        {
            UserId = "MyUser",
            ShowtimeId = showtime.Id,
            BookingReference = "HIVE-OLD",
            TotalAmount = 10,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };
        var newerTicket = new Ticket
        {
            UserId = "MyUser",
            ShowtimeId = showtime.Id,
            BookingReference = "HIVE-NEW",
            TotalAmount = 10,
            CreatedAtUtc = DateTime.UtcNow
        };
        var otherUserTicket = new Ticket
        {
            UserId = "OtherUser",
            ShowtimeId = showtime.Id,
            BookingReference = "HIVE-OTHER",
            TotalAmount = 10,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Tickets.AddRange(olderTicket, newerTicket, otherUserTicket);
        await dbContext.SaveChangesAsync();

        // Act
        var result = (await service.GetMyTicketsAsync("MyUser")).ToList();

        // Assert
        Assert.Equal(2, result.Count); // Should not see OtherUser's ticket
        Assert.Equal("HIVE-NEW", result[0].BookingReference); // Newest should be first
        Assert.Equal("HIVE-OLD", result[1].BookingReference);
        Assert.Equal("The Matrix", result[0].MovieTitle); // Verifying navigation properties loaded
    }

    // --- 3. TESTS FOR: ConfirmTicketPaymentAsync ---

    [Fact]
    public async Task ConfirmTicketPaymentAsync_ValidPendingTicket_ConfirmsAndSellsSeats()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new TicketService(dbContext, cache);
        var showtime = await SeedStandardShowtimeAsync(dbContext);

        // Seat engine state is set to Reserved (1)
        showtime.SeatAvailabilityState[0] = 1;

        var ticket = new Ticket
        {
            UserId = "UserA",
            ShowtimeId = showtime.Id,
            BookingReference = "HIVE-PAY",
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

        // Populate the cache to test that confirmation destroys the cache
        cache.Set($"SeatMap_{showtime.Id}", new ShowtimeSeatMapResponse("M", "C", "A", 10, 10, []));

        // Act
        await service.ConfirmTicketPaymentAsync("HIVE-PAY");

        // Assert
        var updatedTicket = await dbContext.Tickets.FirstAsync(t => t.Id == ticket.Id);
        var updatedShowtime = await dbContext.Showtimes.FirstAsync(s => s.Id == showtime.Id);

        Assert.Equal(TicketStatus.Confirmed, updatedTicket.Status);
        Assert.NotNull(updatedTicket.PaidAtUtc);

        // Byte should flip from 1 (Reserved) to 2 (Sold)
        Assert.Equal(2, updatedShowtime.SeatAvailabilityState[0]);

        // Cache must be cleared
        Assert.False(cache.TryGetValue($"SeatMap_{showtime.Id}", out _));
    }

    [Fact]
    public async Task ConfirmTicketPaymentAsync_TicketAlreadyConfirmed_ReturnsSilentlyIdempotent()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new TicketService(dbContext, cache);
        var showtime = await SeedStandardShowtimeAsync(dbContext);

        var ticket = new Ticket
        {
            UserId = "UserA",
            ShowtimeId = showtime.Id,
            BookingReference = "HIVE-IDEM",
            Status = TicketStatus.Confirmed, // Already Confirmed!
            PaidAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };

        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        // Act
        var exception = await Record.ExceptionAsync(() => service.ConfirmTicketPaymentAsync("HIVE-IDEM"));

        // Assert
        Assert.Null(exception); // Should not throw an error, just return safely
    }

    [Fact]
    public async Task ConfirmTicketPaymentAsync_TicketExpired_ThrowsInvalidOperation()
    {
        // Arrange
        var (dbContext, cache) = GetTestInfrastructure(Guid.NewGuid().ToString());
        var service = new TicketService(dbContext, cache);
        var showtime = await SeedStandardShowtimeAsync(dbContext);

        var ticket = new Ticket
        {
            UserId = "UserA", ShowtimeId = showtime.Id, BookingReference = "HIVE-LATE", Status = TicketStatus.Expired // The cleanup worker killed it!
        };

        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmTicketPaymentAsync("HIVE-LATE"));
        Assert.Contains("cannot be confirmed", ex.Message);
    }
}