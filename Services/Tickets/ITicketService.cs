using Hive_Movie.DTOs;
namespace Hive_Movie.Services.Tickets;

public interface ITicketService
{
    Task<TicketCheckoutResponse> ReserveTicketsAsync(ReserveTicketRequest request, string currentUserId);
    Task<IEnumerable<MyTicketResponse>> GetMyTicketsAsync(string currentUserId);
}