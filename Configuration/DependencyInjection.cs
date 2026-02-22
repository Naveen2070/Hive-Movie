using Hive_Movie.Services.CurrentUser;
using Hive_Movie.Services.Movies;
using Hive_Movie.Services.ShowTimes;

namespace Hive_Movie.Configuration
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Core Services
            services.AddScoped<ICurrentUserService, DummyUserService>();
            services.AddScoped<IShowtimeService, ShowtimeService>();
            services.AddScoped<IMovieService, MovieService>();
            return services;
        }
    }
}
