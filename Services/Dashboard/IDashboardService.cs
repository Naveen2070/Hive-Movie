using Hive_Movie.DTOs;
namespace Hive_Movie.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardStatsResponse> GetOrganizerStatsAsync(string organizerId);
}