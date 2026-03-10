using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Json;
namespace Hive_Movie.Infrastructure.Security;

public class MultiTenantClaimsTransformation(ILogger<MultiTenantClaimsTransformation> logger) : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // If not authenticated, move on
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity)
        {
            return Task.FromResult(principal);
        }

        // IClaimsTransformation can trigger multiple times per request. 
        // This ensures we only add the roles once.
        if (identity.HasClaim(c => c.Type == "MultiTenantTransformed"))
        {
            return Task.FromResult(principal);
        }

        var permissionsClaim = identity.FindFirst("permissions");
        if (permissionsClaim == null)
        {
            logger.LogWarning("JWT accepted, but MISSING 'permissions' claim. Rejecting movie domain access.");
            // We return WITHOUT adding the "MultiTenantTransformed" claim.
            // This means the user has zero movie roles and will be rejected.
            return Task.FromResult(principal);
        }

        try
        {
            // The .NET JWT middleware parses nested JSON objects as JSON strings.
            var permissionsDict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(permissionsClaim.Value);

            if (permissionsDict != null && permissionsDict.TryGetValue("movies", out var movieRoles) && movieRoles.Count > 0)
            {
                foreach (var role in movieRoles)
                {
                    // Strip the "ROLE_" prefix that Spring Boot adds.
                    // This allows us to use [Authorize(Roles = "ORGANIZER")] in our controllers.
                    var cleanRole = role.StartsWith("ROLE_")
                        ? role.Substring(5)
                        : role;

                    identity.AddClaim(new Claim(ClaimTypes.Role, cleanRole));
                }
            }
            else
            {
                logger.LogWarning("JWT accepted, but user has NO permissions for the 'movies' domain. Access denied for Movie API.");
                // Return without marking as transformed. User has no roles.
                return Task.FromResult(principal);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse multi-tenant permissions claim.");
            return Task.FromResult(principal);
        }

        // Mark this identity as transformed ONLY if they successfully got movie roles
        identity.AddClaim(new Claim("MultiTenantTransformed", "true"));

        return Task.FromResult(principal);
    }
}