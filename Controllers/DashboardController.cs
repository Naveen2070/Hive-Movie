using Hive_Movie.DTOs;
using Hive_Movie.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Hive_Movie.Controllers;

/// <summary>
///     Provides statistical aggregates and metrics for the Organizer Command Center.
/// </summary>
[Route("api/movies/[controller]")]
[ApiController]
[Tags("Dashboard Analytics")]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>
    ///     Retrieves aggregated dashboard statistics for the authenticated Organizer.
    /// </summary>
    /// <remarks>
    ///     Returns calculated revenue trends, ticket sales metrics, and recent transactions
    ///     formatted specifically for the React frontend's Recharts integration.
    /// </remarks>
    /// <response code="200">Returns the populated DashboardStatsDTO.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user is not an organizer.</response>
    [Authorize(Roles = "ORGANIZER,SUPER_ADMIN")]
    [HttpGet]
    [ProducesResponseType(typeof(DashboardStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var organizerId = User.FindFirst("id")?.Value
            ?? throw new UnauthorizedAccessException("Missing User ID.");

        var stats = await dashboardService.GetOrganizerStatsAsync(organizerId);

        return Ok(stats);
    }
}