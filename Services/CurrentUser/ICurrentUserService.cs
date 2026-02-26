using Hive_Movie.DTOs;
namespace Hive_Movie.Services.CurrentUser;

public interface ICurrentUserService
{
    string? UserId { get; }

    CurrentUserDetails? GetCurrentUser();
}