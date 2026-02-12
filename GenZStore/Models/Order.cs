namespace GenZStore.Models
{
    public class Order
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? PaymentMethod { get; set; }
        public string Status { get; set; } // Pending, Done
        public decimal TotalAmount { get; set; }
        public string Address { get; set; }
        public string LocationLink { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; }
        public ICollection<Transaction> Transactions { get; set; }
    }
}
