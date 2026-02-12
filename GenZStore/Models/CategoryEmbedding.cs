using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GenZStore.Models
{
    public class CategoryEmbedding
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        // Navigation property to link back to the Category
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        public string ModelName { get; set; } = "text-embedding-004";

        [Required]
        public string Embedding { get; set; } // Stores the JSON vector

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}