using Hive_Movie.Data;
using Hive_Movie.DTOs;
using Hive_Movie.Models;
using Hive_Movie.Services.CurrentUser;
using Hive_Movie.Services.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
namespace Tests.Integration;

[Collection("Database collection")]
public class SeatConcurrencyTests(SqlServerFixture fixture) : IAsyncLifetime
{
    // This runs BEFORE every single [Fact] in this file, ensuring a clean slate!
    public Task InitializeAsync()
    {
        return fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private ApplicationDbContext CreateContext(string userId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.UserId).Returns(userId);

        return new ApplicationDbContext(options, mockUser.Object);
    }

    [Fact]
    public async Task ReserveTickets_TwoUsersSameSeat_OneFailsWithConcurrencyException()
    {
        // ---------------------------------------------------------
        // 1. Setup the Database (MUST INCLUDE FULL HIERARCHY)
        // ---------------------------------------------------------
        var setupContext = CreateContext("System");

        // We must create the parents first so SQL Server Foreign Keys are happy!
        var movie = new Movie
        {
            Id = Guid.NewGuid(), Title = "Concurrency Movie", Description = "Concurrency Movie Description", DurationMinutes = 120
        };
        var cinema = new Cinema
        {
            Id = Guid.NewGuid(),
            Name = "Concurrency Cinema",
            ContactEmail = "test@hive.com",
            Location = "Test",
            OrganizerId = "Org1"
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
            SeatAvailabilityState = new byte[100] // 100 empty seats
        };

        // Add them ALL to the database
        setupContext.Movies.Add(movie);
        setupContext.Cinemas.Add(cinema);
        setupContext.Auditoriums.Add(auditorium);
        setupContext.Showtimes.Add(showtime);

        // This will now pass the SQL Server Foreign Key checks!
        await setupContext.SaveChangesAsync();

        // ---------------------------------------------------------
        // 2. Simulate Two Concurrent Web Requests
        // ---------------------------------------------------------
        var contextA = CreateContext("UserA");
        var contextB = CreateContext("UserB");

        var serviceA = new TicketService(contextA, new MemoryCache(new MemoryCacheOptions()));
        var serviceB = new TicketService(contextB, new MemoryCache(new MemoryCacheOptions()));

        var request = new ReserveTicketRequest(showtime.Id, [new SeatCoordinateDto(0, 0)]);

        // ---------------------------------------------------------
        // 3. Fire both requests at the EXACT SAME MILLISECOND
        // ---------------------------------------------------------
        var taskA = serviceA.ReserveTicketsAsync(request, "UserA");
        var taskB = serviceB.ReserveTicketsAsync(request, "UserB");

        Exception? caughtException = null;
        try
        {
            await Task.WhenAll(taskA, taskB);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // ---------------------------------------------------------
        // 4. Assert the Optimistic Concurrency Lock Worked!
        // ---------------------------------------------------------
        Assert.NotNull(caughtException);
        Assert.IsType<DbUpdateConcurrencyException>(caughtException);

        // Prove only ONE ticket made it into the database
        var verifyContext = CreateContext("System");
        var totalTickets = await verifyContext.Tickets.CountAsync();

        Assert.Equal(1, totalTickets);
    }
}