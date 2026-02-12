using GenZStore.Commands;
using GenZStore.DTOs;
using GenZStore.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using GenZStore.Hubs;
using Microsoft.AspNetCore.Authorization;

namespace GenZStore.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderQuery _orderQuery;
        private readonly IOrderCommand _orderCommand;
        private readonly IHubContext<OrderHub> _hubContext;

        public OrdersController(IOrderQuery orderQuery, IOrderCommand orderCommand, IHubContext<OrderHub> hubContext)
        {
            _orderQuery = orderQuery;
            _orderCommand = orderCommand;
            _hubContext = hubContext;
        }

        // GET: api/orders (See all orders for Dashboard)

        [HttpGet]
        public async Task<IActionResult> GetAllOrders(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string status = "All",
            [FromQuery] string search = "")
        {
            var result = await _orderQuery.GetOrdersAsync(page, pageSize, status, search);
            return Ok(result);
        }

        // GET: api/orders/{id} (See specific receipt)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderById(Guid id)
        {
            var order = await _orderQuery.GetOrderByIdAsync(id);
            return order == null ? NotFound() : Ok(order);
        }

        // PUT: api/orders/{id}/status (Update: Pending -> Delivering -> Done)
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusDto dto)
        {
            var success = await _orderCommand.UpdateStatusAsync(id, dto.NewStatus);

            if (!success) return BadRequest("Order not found or Invalid Status");

            return Ok(new { Message = $"Order status updated to {dto.NewStatus}" });
        }

        // DELETE: api/orders/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(Guid id)
        {
            var success = await _orderCommand.DeleteOrderAsync(id);
            return success ? Ok(new { Message = "Order deleted" }) : NotFound();
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            var orderId = await _orderCommand.CreateOrderAsync(dto);

            // ✅ REAL-TIME MAGIC: Notify all admins
            await _hubContext.Clients.All.SendAsync("ReceiveOrder", new
            {
                OrderId = orderId,
                Customer = dto.CustomerName,
                Total = 0, // Calculate this if possible
                Status = "Pending"
            });

            return Ok(new { OrderId = orderId, Message = "Order placed!" });
        }

        // ✅ [CLIENT] Track My Orders
        [HttpGet("my-orders")]
        public async Task<IActionResult> GetMyOrders([FromQuery] string telegramId)
        {
            // Filter orders where Customer.TelegramId matches
            // You need to update IOrderQuery for this
            var orders = await _orderQuery.GetOrdersByTelegramIdAsync(telegramId);
            return Ok(orders);
        }
    }
}