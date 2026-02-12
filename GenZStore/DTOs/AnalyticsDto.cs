namespace GenZStore.DTOs
{
    public class DashboardSummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int LowStockProducts { get; set; }
    }

    public class RevenueChartDto
    {
        public string Date { get; set; } // "Mon", "Tue" or "2026-02-08"
        public decimal Revenue { get; set; }
    }

    public class AiInsightDto
    {
        public string Question { get; set; }
        public string Answer { get; set; }
        public string TimeAgo { get; set; }
    }
}