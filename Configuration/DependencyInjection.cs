using FluentValidation;
using Hive_Movie.Services.Auditoriums;
using Hive_Movie.Services.Cinemas;
using Hive_Movie.Services.CurrentUser;
using Hive_Movie.Services.Movies;
using Hive_Movie.Services.ShowTimes;
using Hive_Movie.Services.Tickets;
using Hive_Movie.Services.Workers;
namespace Hive_Movie.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Add HttpContextAccessor for CurrentUserService
        services.AddHttpContextAccessor();

        // Enable High-performance cache
        services.AddMemoryCache();

        // Core Services
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IShowtimeService, ShowtimeService>();
        services.AddScoped<IMovieService, MovieService>();
        services.AddScoped<ICinemaService, CinemaService>();
        services.AddScoped<IAuditoriumService, AuditoriumService>();
        services.AddScoped<ITicketService, TicketService>();

        // FluentValidation
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Workers
        services.AddHostedService<TicketCleanupWorker>();

        return services;
    }
}