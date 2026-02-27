using FluentValidation;
using Hive_Movie.Infrastructure.Clients;
using Hive_Movie.Infrastructure.Messaging;
using Hive_Movie.Infrastructure.Security;
using Hive_Movie.Services.Auditoriums;
using Hive_Movie.Services.Cinemas;
using Hive_Movie.Services.CurrentUser;
using Hive_Movie.Services.Movies;
using Hive_Movie.Services.ShowTimes;
using Hive_Movie.Services.Tickets;
using Hive_Movie.Workers;
using Refit;
// Make sure this is imported!

namespace Hive_Movie.Configuration;

public static class DependencyInjection
{
    // ADD IConfiguration as the second parameter here!
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
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

        // Messaging and inter service communication
        services.AddSingleton<INotificationProducer, NotificationProducer>();

        services.AddTransient<S2SAuthenticationHandler>();

        services.AddRefitClient<IIdentityClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(configuration["IdentityService:Url"] ?? "http://localhost:8081"))
            .AddHttpMessageHandler<S2SAuthenticationHandler>();

        return services;
    }
}