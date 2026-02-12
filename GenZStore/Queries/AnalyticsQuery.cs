using GenZStore.Data;
using GenZStore.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GenZStore.Queries
{
    public interface IAnalyticsQuery
    {
        Task<DashboardSummaryDto> GetSummaryAsync();
        Task<List<RevenueChartDto>> GetWeeklyRevenueAsync();
        Task<List<AiInsightDto>> GetRecentAiInteractionsAsync();
    }

    public class AnalyticsQuery : IAnalyticsQuery
    {
        private readonly AppDbContext _context;

        public AnalyticsQuery(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardSummaryDto> GetSummaryAsync()
        {
            var today = DateTime.UtcNow.Date;

            return new DashboardSummaryDto
            {
                // Money made (Only count "Paid" or "Done" orders if you want actual cash)
                TotalRevenue = await _context.Orders
                    .Where(o => o.Status != "Cancelled")
                    .SumAsync(o => o.TotalAmount),

                TotalOrders = await _context.Orders.CountAsync(),

                PendingOrders = await _context.Orders
                    .CountAsync(o => o.Status == "Pending"),

                // Products with less than 5 items left
                LowStockProducts = await _context.Products
                    .CountAsync(p => p.Quantity < 5)
            };
        }

        public async Task<List<RevenueChartDto>> GetWeeklyRevenueAsync()
        {
            // Get data for the last 7 days
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            var salesData = await _context.Orders
                .Where(o => o.CreatedAt >= sevenDaysAgo && o.Status != "Cancelled")
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Sum(o => o.TotalAmount)
                })
                .ToListAsync();

            // Fill in missing days with $0 (Dashboard charts hate gaps)
            var result = new List<RevenueChartDto>();
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.UtcNow.AddDays(-i).Date;
                var sale = salesData.FirstOrDefault(s => s.Date == date);

                result.Add(new RevenueChartDto
                {
                    Date = date.ToString("dd/MM"), // Format: "08/02"
                    Revenue = sale?.Total ?? 0
                });
            }

            return result;
        }

        public async Task<List<AiInsightDto>> GetRecentAiInteractionsAsync()
        {
            return await _context.ChatLogs
                .OrderByDescending(c => c.CreatedAt)
                .Take(10) // Show last 10 questions
                .Select(c => new AiInsightDto
                {
                    Question = c.UserQuestion,
                    Answer = c.AiAnswer.Length > 50 ? c.AiAnswer.Substring(0, 50) + "..." : c.AiAnswer,
                    TimeAgo = GetTimeAgo(c.CreatedAt)
                })
                .ToListAsync();
        }

        // Helper for "2 mins ago"
        private static string GetTimeAgo(DateTime date)
        {
            var span = DateTime.UtcNow - date;
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }
    }
}