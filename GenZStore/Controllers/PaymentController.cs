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
        private readonly PayWayService _payWayService;
        private readonly AppDbContext _context;

        public PaymentController(PayWayService payWayService, AppDbContext context)
        {
            _payWayService = payWayService;
            _context = context;
        }

        // 1. GENERATE QR (Frontend calls this first)
        [HttpPost("generate-payway")]
        public async Task<IActionResult> GeneratePayWayQr([FromBody] PaymentRequestDto request)
        {
            try
            {
                var result = await _payWayService.CreateKhqrTransactionAsync(request.OrderId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // 2. POLLING ENDPOINT (Frontend calls this every 3s)
        [HttpGet("check-status/{orderId}")]
        public async Task<IActionResult> CheckStatus(Guid orderId)
        {
            var tranId = orderId.ToString("N").Substring(0, 20);

            // Ask ABA if it's paid
            bool isPaid = await _payWayService.CheckTransactionStatusAsync(tranId);

            if (isPaid)
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order != null && order.Status == "Pending")
                {
                    order.Status = "Paid";
                    order.PaidAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
                return Ok(new { status = "PAID" });
            }

            return Ok(new { status = "PENDING" });
        }

        // 3. WEBHOOK (ABA calls this automatically)
        // URL: https://api.yoursite.com/api/payment/payway-callback
        [HttpPost("payway-callback")]
        public async Task<IActionResult> PayWayCallback([FromForm] IFormCollection form)
        {
            try
            {
                var status = form["status"];
                var tranId = form["tran_id"];

                // SECURITY CHECK (Crucial for CV/Interview)
                // In a real interview, you must mention verifying the hash!
                // var isValid = _payWayService.ValidateWebhookHash(tranId, form["amount"], form["req_time"], form["hash"]);
                // if (!isValid) return Unauthorized();

                if (status == "00")
                {
                    // Using StartsWith because tranId is a substring of GUID
                    var order = await _context.Orders
                        .FirstOrDefaultAsync(o => o.Id.ToString().StartsWith(tranId));

                    if (order != null && order.Status != "Paid")
                    {
                        order.Status = "Paid";
                        order.PaidAt = DateTime.Now;
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"[Webhook] Order {tranId} confirmed!");
                    }
                }
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500);
            }
        }
    }
}