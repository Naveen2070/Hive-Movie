using Hive_Movie.DTOs;
using System.Security.Claims;
namespace Hive_Movie.Services.CurrentUser;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? UserId => GetCurrentUser()?.Id;

    public CurrentUserDetails? GetCurrentUser()
    {
        var user = httpContextAccessor.HttpContext?.User;

        // If there is no token, or the token is invalid, return null
        if (user == null || user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // 1. Extract ID
        var id = user.FindFirst("id")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? string.Empty;

        // 2. Extract Email (Kotlin uses userDetails.username which is the email)
        var email = user.FindFirst("email")?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? string.Empty;

        // 3. Extract Roles
        // .NET automatically splits a JSON array like ["ROLE_USER", "ROLE_ADMIN"] into multiple individual claims!
        var roles = user.FindAll("roles").Select(c => c.Value)
            .Concat(user.FindAll(ClaimTypes.Role).Select(c => c.Value))
            .Distinct()
            .ToList();

        // If we can't find an ID, we don't have a valid user context for the Hive ecosystem
        return string.IsNullOrEmpty(id)
            ? null
            : new CurrentUserDetails(id, email, roles);
    }
}