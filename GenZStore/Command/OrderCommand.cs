using GenZStore.Data;
using GenZStore.DTOs;
using GenZStore.Models;
using Microsoft.EntityFrameworkCore;

namespace GenZStore.Commands
{
    public interface IOrderCommand
    {
        Task<bool> UpdateStatusAsync(Guid orderId, string newStatus);
        Task<bool> DeleteOrderAsync(Guid orderId); // Only for junk orders
        Task<Guid> CreateOrderAsync(CreateOrderDto dto);
    }

    public class OrderCommand : IOrderCommand
    {
        private readonly AppDbContext _context;

        public OrderCommand(AppDbContext context)
        {
            _context = context;
        }
        public async Task<Guid> CreateOrderAsync(CreateOrderDto dto)
        {
            // 1. Find or Create Customer based on TelegramId
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.TelegramId == dto.TelegramId);

            if (customer == null)
            {
                // If this is their first time ordering, create a Customer record
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    TelegramId = dto.TelegramId,
                    Name = dto.CustomerName,
                    Phone = dto.Phone,
                    CreatedAt = DateTime.UtcNow
                    // Note: You might need to link this to ApplicationUser here if strictly required
                };
                await _context.Customers.AddAsync(customer);
            }

            // 2. Create the Order
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Customer = customer,
                Address = dto.Address,
                LocationLink = dto.LocationLink,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OrderItems = new List<OrderItem>()
            };

            decimal totalAmount = 0;

            // 3. Process Items
            foreach (var itemDto in dto.Items)
            {
                var product = await _context.Products.FindAsync(itemDto.ProductId);
                if (product == null) continue; // Skip invalid products

                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Product = product,
                    Quantity = itemDto.Quantity,
                    UnitPrice = product.Price // Always take price from DB
                };

                order.OrderItems.Add(orderItem);
                totalAmount += (product.Price * itemDto.Quantity);
            }

            order.TotalAmount = totalAmount;

            // 4. Save everything
            try
            {
                await _context.Orders.AddAsync(order);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Check your terminal for this output!
                Console.WriteLine($"DB Error: {ex.Message}");
                if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
                throw; // Re-throw so the controller sees the 500
            }
            return order.Id;
        }
        public async Task<bool> UpdateStatusAsync(Guid orderId, string newStatus)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return false;

            // Valid status checks (optional but good for safety)
            var validStatuses = new[] { "Pending", "Paid", "Delivering", "Done", "Cancelled" };
            if (!validStatuses.Contains(newStatus)) return false;

            order.Status = newStatus;
            order.UpdatedAt = DateTime.UtcNow;

            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteOrderAsync(Guid orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return false;

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}