namespace GenZStore.Models
{
    public class ProductEmbedding
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Product Product { get; set; }

        public string ModelName { get; set; } // qwen3
        public string Embedding { get; set; } // JSON array of floats
        public DateTime CreatedAt { get; set; }
    }
}
