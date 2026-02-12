using Dapper;
using GenZStore.Data;
using GenZStore.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GenZStore.Queries
{
    public interface IOrderQuery
    {
        // ✅ UPDATE: Return PagedResult and accept search/pagination params
        Task<PagedResult<OrderDto>> GetOrdersAsync(int page, int pageSize, string status, string search);

        Task<OrderDto?> GetOrderByIdAsync(Guid id);
        Task<List<OrderDto>> GetOrdersByTelegramIdAsync(string telegramId);
    }

    public class OrderQuery : IOrderQuery
    {
        private readonly AppDbContext _context;
        private readonly IDbConnectionFactory _db;

        public OrderQuery(AppDbContext context, IDbConnectionFactory db)
        {
            _context = context;
            _db = db;
        }

        // ✅ DAPPER: Paginated List + Search + Filter
        public async Task<PagedResult<OrderDto>> GetOrdersAsync(int page, int pageSize, string status, string search)
        {
            using var conn = _db.CreateConnection();

            // Dynamic WHERE clause builder
            var sqlWhere = "WHERE 1=1";
            var parameters = new DynamicParameters();

            parameters.Add("Offset", (page - 1) * pageSize);
            parameters.Add("PageSize", pageSize);

            // 1. Filter by Status
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                sqlWhere += " AND o.Status = @Status";
                parameters.Add("Status", status);
            }

            // 2. Filter by Search (Order ID or Customer Name)
            if (!string.IsNullOrEmpty(search))
            {
                sqlWhere += " AND (c.Name LIKE @Search OR CAST(o.Id AS NVARCHAR(50)) LIKE @Search)";
                parameters.Add("Search", $"%{search}%");
            }

            var sql = $@"
                -- 1. Get Paginated Orders
                SELECT 
                    o.Id,
                    c.Name AS CustomerName,
                    c.Phone,
                    o.Address,
                    o.Status,
                    o.TotalAmount,
                    o.CreatedAt,
                    o.LocationLink
                FROM Orders o
                INNER JOIN Customers c ON o.CustomerId = c.Id
                {sqlWhere}
                ORDER BY o.CreatedAt DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

                -- 2. Get Total Count
                SELECT COUNT(*) 
                FROM Orders o
                INNER JOIN Customers c ON o.CustomerId = c.Id
                {sqlWhere};";

            using var multi = await conn.QueryMultipleAsync(sql, parameters);

            var items = await multi.ReadAsync<OrderDto>();
            var totalCount = await multi.ReadFirstAsync<int>();

            return new PagedResult<OrderDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        // ... Keep GetOrderByIdAsync (Dapper) ...
        public async Task<OrderDto?> GetOrderByIdAsync(Guid id)
        {
            using var connection = _db.CreateConnection();

            var sql = @"
                SELECT 
                    o.Id, 
                    o.Status, 
                    o.TotalAmount, 
                    o.CreatedAt, 
                    o.Address, 
                    o.LocationLink,
                    c.Name AS CustomerName, 
                    c.Phone,
                    oi.Quantity, 
                    oi.UnitPrice,
                    p.Name AS ProductName, 
                    p.ImageUrl AS ProductImage
                FROM Orders o
                INNER JOIN Customers c ON o.CustomerId = c.Id
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                WHERE o.Id = @Id";

            var orderDictionary = new Dictionary<Guid, OrderDto>();

            var result = await connection.QueryAsync<OrderDto, OrderItemDto, OrderDto>(
                sql,
                (order, item) =>
                {
                    if (!orderDictionary.TryGetValue(order.Id, out var currentOrder))
                    {
                        currentOrder = order;
                        currentOrder.Items = new List<OrderItemDto>();
                        orderDictionary.Add(currentOrder.Id, currentOrder);
                    }

                    if (item != null && !string.IsNullOrEmpty(item.ProductName))
                    {
                        if (!string.IsNullOrEmpty(item.ProductImage) && !item.ProductImage.StartsWith("http"))
                        {
                            item.ProductImage = $"https://localhost:7171/images/{item.ProductImage}";
                        }
                        currentOrder.Items.Add(item);
                    }
                    return currentOrder;
                },
                new { Id = id },
                splitOn: "Quantity"
            );

            return orderDictionary.Values.FirstOrDefault();
        }

        // ... Keep GetOrdersByTelegramIdAsync (EF Core) ...
        public async Task<List<OrderDto>> GetOrdersByTelegramIdAsync(string telegramId)
        {
            var orders = await _context.Orders
               .AsNoTracking() // Good for performance on read-only queries
               .Where(o => o.Customer.TelegramId == telegramId)
               .OrderByDescending(o => o.CreatedAt) // Show newest orders first
               .Select(o => new OrderDto
               {
                   Id = o.Id,
                   Status = o.Status,
                   TotalAmount = o.TotalAmount,
                   CreatedAt = o.CreatedAt,
                   CustomerName = o.Customer.Name ?? "Guest",

                   // 👇 THIS WAS MISSING
                   Items = o.OrderItems.Select(oi => new OrderItemDto
                   {
                       ProductName = oi.Product.Name,
                       Quantity = oi.Quantity,
                       UnitPrice = oi.UnitPrice,
                       // Handle Image URL if needed
                       ProductImage = oi.Product.ImageUrl
                   }).ToList()
               }).ToListAsync();

            return orders;
        }
    }
}