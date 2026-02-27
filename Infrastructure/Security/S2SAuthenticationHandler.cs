using System.Security.Cryptography;
using System.Text;
namespace Hive_Movie.Infrastructure.Security;

/// <summary>
///     Utility class and HTTP Handler for secure Service-to-Service (S2S) authentication.
///     This implementation uses HMAC-SHA256 signatures combined with a timestamp
///     to provide:
///     • Strong cryptographic integrity (via HMAC)
///     • Replay attack protection (via timestamp validation)
///     • Timing attack resistance (via constant-time comparison)
/// </summary>
public class S2SAuthenticationHandler(IConfiguration configuration) : DelegatingHandler
{
    private const long DefaultAllowedClockSkewSeconds = 60;

    /// <summary>
    ///     Intercepts the outbound HTTP request and automatically attaches the S2S HMAC headers.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var serviceId = configuration["InternalProperties:ServiceId"] ?? "movie-service";
        var sharedSecret = configuration["InternalProperties:SharedSecret"]
            ?? throw new InvalidOperationException("InternalProperties:SharedSecret is missing in appsettings.json!");

        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateSignature(serviceId, currentTimestamp, sharedSecret);

        request.Headers.Add("X-Internal-Service-ID", serviceId);
        request.Headers.Add("X-Service-Timestamp", currentTimestamp.ToString());
        request.Headers.Add("X-Service-Signature", signature);

        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    ///     Generates a Base64-encoded HMAC-SHA256 signature for service-to-service authentication.
    /// </summary>
    private static string GenerateSignature(string serviceId, long timestamp, string sharedSecret)
    {
        var payload = $"{serviceId}:{timestamp}";
        var secretBytes = Encoding.UTF8.GetBytes(sharedSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(secretBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);

        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    ///     Validates a service-to-service authentication token.
    /// </summary>
    public static bool ValidateToken(
        string signature,
        string serviceId,
        long timestamp,
        string sharedSecret,
        long maxAgeSeconds = DefaultAllowedClockSkewSeconds)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 1. Replay Attack Prevention (Is the token too old or from the future?)
        if (Math.Abs(now - timestamp) > maxAgeSeconds)
            return false;

        // 2. Recalculate Expected Signature
        var expectedSignature = GenerateSignature(serviceId, timestamp, sharedSecret);

        // 3. Constant-time comparison to prevent timing side-channel attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(signature)
        );
    }
}