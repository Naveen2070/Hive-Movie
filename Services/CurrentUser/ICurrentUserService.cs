namespace Hive_Movie.Services.CurrentUser
{
    public interface ICurrentUserService
    {
        // Extracts the User ID from the incoming JWT Token
        string? UserId { get; }
    }
}
