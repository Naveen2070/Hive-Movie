using Hive_Movie.DTOs;
using Refit;
namespace Hive_Movie.Infrastructure.Clients;

public interface IIdentityClient
{
    [Get("/api/internal/users/{id}")]
    Task<UserSummaryDto> GetUserByIdAsync(long id);

    [Post("/api/internal/users/batch")]
    Task<List<UserSummaryDto>> GetUsersByIdsAsync([Body] List<long> ids);
}