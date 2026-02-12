using System.ComponentModel.DataAnnotations;

namespace GenZStore.Models
{
    public class Customer
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } 

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Phone { get; set; }
        public string TelegramId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Order>? Orders { get; set; }
    }
}
