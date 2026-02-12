namespace GenZStore.DTOs
{
    public class OrderDto
    {
        public Guid Id { get; set; }
        public string CustomerName { get; set; } // From Customer table
        public string Phone { get; set; }        // From Customer table
        public string Address { get; set; }
        public string LocationLink { get; set; }
        public string Status { get; set; }       // Pending, Delivering, Done
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }

        // List of items in this order
        public List<OrderItemDto> Items { get; set; }
    }

    public class OrderItemDto
    {
        public string ProductName { get; set; }
        public string ProductImage { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice => Quantity * UnitPrice;
    }

    public class UpdateOrderStatusDto
    {
        public string NewStatus { get; set; } // "Delivering", "Done", "Cancelled"
    }
    public class OrderFilterDto
    {
        public string? Status { get; set; }        // "Pending", "Done"
        public DateTime? FromDate { get; set; }    // Start Date
        public DateTime? ToDate { get; set; }      // End Date
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        public string? SortBy { get; set; }        // "date", "price", "status", "total"
        public bool IsDescending { get; set; } = true; // Default: Newest first
    }
    public class CreateOrderDto
    {
        public string TelegramId { get; set; } // To link order to user
        public string CustomerName { get; set; } // Fallback if name is new
        public string Phone { get; set; }
        public string Address { get; set; }
        public string LocationLink { get; set; }
        public string PaymentMethod { get; set; } // "Bakong", "Cash"

        public List<CreateOrderItemDto> Items { get; set; }
    }

    public class CreateOrderItemDto
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }
}