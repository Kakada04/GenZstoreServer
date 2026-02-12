using GenZStore.Queries;
using Microsoft.AspNetCore.Mvc;

namespace GenZStore.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsQuery _analytics;

        public AnalyticsController(IAnalyticsQuery analytics)
        {
            _analytics = analytics;
        }

        // GET: api/analytics/summary
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var summary = await _analytics.GetSummaryAsync();
            return Ok(summary);
        }

        // GET: api/analytics/chart
        [HttpGet("chart")]
        public async Task<IActionResult> GetChartData()
        {
            var chart = await _analytics.GetWeeklyRevenueAsync();
            return Ok(chart);
        }

        // GET: api/analytics/insights (What are people asking?)
        [HttpGet("insights")]
        public async Task<IActionResult> GetAiInsights()
        {
            var insights = await _analytics.GetRecentAiInteractionsAsync();
            return Ok(insights);
        }
    }
}