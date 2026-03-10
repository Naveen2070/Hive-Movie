namespace Hive_Movie.DTOs;

public record RevenueTrendItem(
    string Date,
    decimal Revenue
);

public record RecentSale(
    string Id,
    string EventName,
    string CustomerName,
    string TierName,
    int Tickets,
    decimal Amount,
    string Date
);

public record DashboardStatsResponse(
    decimal TotalRevenue,
    double RevenueGrowthLastWeekPercent,
    double RevenueGrowthLastMonthPercent,
    int TotalTicketsSold,
    int PendingPaymentTickets,
    int TicketsSoldLastWeek,
    int ActiveEvents,
    int TotalEvents,
    List<RevenueTrendItem> RevenueTrend,
    List<RecentSale> RecentSales
);