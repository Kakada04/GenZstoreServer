using Microsoft.AspNetCore.Mvc;
using GenZStore.Services;
using GenZStore.Data;
using GenZStore.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GenZStore.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly BakongService _bakongService;
        private readonly AppDbContext _context;

        public PaymentController(BakongService bakongService, AppDbContext context)
        {
            _bakongService = bakongService;
            _context = context;
        }

        [HttpPost("generate-bakong")]
        public async Task<IActionResult> GenerateBakongQr([FromBody] PaymentRequestDto request)
        {
            try
            {
                var result = await _bakongService.GenerateKhqrAsync(request.OrderId);

                // 🚨 CRITICAL FIX: Ensure we never send a 200 OK with null body
                if (result == null)
                {
                    return BadRequest(new { error = "Failed to generate QR. Ensure order exists and Bakong Account is valid." });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                // This catches the SDK exceptions we threw in the Service
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("check-status/{orderId}/{md5}")]
        public async Task<IActionResult> CheckStatus(Guid orderId, string md5)
        {
            bool isPaid = await _bakongService.CheckPaymentStatusAsync(md5);

            if (isPaid)
            {
                var order = await _context.Orders.FindAsync(orderId);
                // Double check to prevent updating already paid orders
                if (order != null && order.Status != "Paid")
                {
                    order.Status = "Paid";
                    order.PaidAt = DateTime.UtcNow; // Use UtcNow for consistency
                    await _context.SaveChangesAsync();
                }
                return Ok(new { status = "PAID" });
            }

            return Ok(new { status = "PENDING" });
        }

        // ... (Webhook remains the same)
    }
}