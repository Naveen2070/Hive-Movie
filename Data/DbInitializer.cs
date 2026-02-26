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

        // 2. Create a Cinema (Owned by an Organizer)
        var cinema = new Cinema
        {
            Name = "Hive Multiplex Downtown",
            Location = "123 Tech Boulevard",
            ApprovalStatus = CinemaApprovalStatus.Approved,
            ContactEmail = "org@email.com",
            OrganizerId = "john.doe@example.com" // A realistic JWT 'sub' email
        };
        context.Cinemas.Add(cinema);

        // 3. Create an Auditorium (10 rows, 15 columns = 150 seats)
        // Let's build a realistic JSON layout with Tiers and special seats!
        var layout = new AuditoriumLayout
        {
            // Missing seats in the front corners (Row 0, Col 0 and Row 0, Col 14)
            DisabledSeats = 
            [
                new SeatCoordinate { Row = 0, Col = 0 },
                new SeatCoordinate { Row = 0, Col = 14 }
            ],
            
            // Wheelchair accessible spots at the back corners
            WheelchairSpots = 
            [
                new SeatCoordinate { Row = 9, Col = 0 },
                new SeatCoordinate { Row = 9, Col = 14 }
            ],
            
            // Define the Premium VIP Tier with a $5.00 surcharge
            Tiers = 
            [
                new SeatTier
                {
                    TierName = "VIP Recliners",
                    PriceSurcharge = 5.00m,
                    // The premium center seats in Row 8
                    Seats = 
                    [
                        new SeatCoordinate { Row = 8, Col = 5 },
                        new SeatCoordinate { Row = 8, Col = 6 },
                        new SeatCoordinate { Row = 8, Col = 7 },
                        new SeatCoordinate { Row = 8, Col = 8 },
                        new SeatCoordinate { Row = 8, Col = 9 }
                    ]
                }
            ]
        };

        var auditorium = new Auditorium
        {
            CinemaId = cinema.Id, 
            Name = "IMAX Screen 1",
            MaxRows = 10,
            MaxColumns = 15,
            LayoutConfiguration = layout, 
            Cinema = cinema
        };
        context.Auditoriums.Add(auditorium);

        // 4. Create a Showtime
        var totalSeats = auditorium.MaxRows * auditorium.MaxColumns;

        var showtime = new Showtime
        {
            MovieId = movie.Id,
            AuditoriumId = auditorium.Id,
            StartTimeUtc = DateTime.UtcNow.AddDays(2), 
            BasePrice = 15.99m, 
            
            // Initialize the 150 seats to 0 (Available)
            SeatAvailabilityState = new byte[totalSeats],

            Movie = movie,
            Auditorium = auditorium
        };
        context.Showtimes.Add(showtime);

        // 5. Save everything to SQL Server
        context.SaveChanges();
    }
}