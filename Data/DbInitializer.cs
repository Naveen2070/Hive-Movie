using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;

namespace Hive_Movie.Data;

public static class DbInitializer
{
    public static void Initialize(ApplicationDbContext context)
    {
        // Apply any pending migrations automatically on startup!
        context.Database.Migrate();

        // If we already have movies, the database has been seeded. Do nothing.
        if (context.Movies.Any())
        {
            return;
        }

        // 1. Create a Movie
        var movie = new Movie
        {
            Title = "The Polyglot Matrix",
            Description = "A developer discovers the truth about microservices.",
            DurationMinutes = 136,
            ReleaseDate = DateTime.UtcNow.AddDays(-10),
            PosterUrl = "https://example.com/matrix-poster.jpg"
        };
        context.Movies.Add(movie);

        // 2. Create a Cinema
        var cinema = new Cinema
        {
            Name = "Hive Multiplex Downtown",
            Location = "123 Tech Boulevard"
        };
        context.Cinemas.Add(cinema);

        // 3. Create an Auditorium (10 rows, 15 columns = 150 seats)
        var auditorium = new Auditorium
        {
            CinemaId = cinema.Id, // Link to the Cinema we just created
            Name = "IMAX Screen 1",
            MaxRows = 10,
            MaxColumns = 15,
            Cinema = cinema
        };
        context.Auditoriums.Add(auditorium);

        // 4. Create a Showtime
        var totalSeats = auditorium.MaxRows * auditorium.MaxColumns;

        var showtime = new Showtime
        {
            MovieId = movie.Id,
            AuditoriumId = auditorium.Id,
            StartTimeUtc = DateTime.UtcNow.AddDays(2), // Playing in 2 days
            BasePrice = 15.99m,
            // Initialize the 150 seats to 0 (Available)
            SeatAvailabilityState = new byte[totalSeats],

            Movie = movie,
            Auditorium = auditorium
        };
        context.Showtimes.Add(showtime);

        // 5. Save everything to SQL Server
        // (Our EF Core ChangeTracker interceptor will automatically add the CreatedAt/CreatedBy fields!)
        context.SaveChanges();
    }
}