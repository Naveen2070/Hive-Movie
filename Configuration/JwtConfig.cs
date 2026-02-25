using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Hive_Movie.Configuration;

public static class JwtConfig
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Grab the secret key from appsettings.json
        var jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT Secret is missing from configuration.");

        // 2. Convert the secret string into a byte array for the crypto engine
        var key = Convert.FromBase64String(jwtSecret);

        // 3. Register the Authentication middleware
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // YES: Mathematically verify the token hasn't been tampered with
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),

                    // YES: Reject the token if the 15-minute expiration has passed
                    ValidateLifetime = true,
                    // By default, .NET allows a 5-minute "clock skew" grace period. 
                    // Let's set it to zero so it perfectly matches your Kotlin logic.
                    ClockSkew = TimeSpan.Zero,

                    // NO: We don't have these, so tell .NET not to crash if they are missing
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    // YES: Map the "roles" claim in the JWT to User.IsInRole() checks
                    RoleClaimType = "roles",
                    NameClaimType = "sub"
                };
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\nJWT VALIDATION FAILED: {context.Exception.Message}\n");
                        Console.ResetColor();
                        return Task.CompletedTask;
                    }
                };
            });

        // 4. Register Authorization capabilities (needed for the [Authorize] attributes)
        services.AddAuthorization();

        return services;
    }
}
