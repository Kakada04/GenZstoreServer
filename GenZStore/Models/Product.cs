namespace GenZStore.Models
{
    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; } 
        public string ImageUrl { get; set; } 
        public string Description { get; set; }
        public string Usage { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }

        public Guid CategoryId { get; set; }
        public Category Category { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; }
    }
}
