using System.Text.Json.Serialization;

namespace Hive_Movie.DTOs;

/// <summary>
/// Standardized API error response matching the Spring Boot services.
/// </summary>
public class ApiErrorResponse
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}
