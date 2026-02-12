using Microsoft.AspNetCore.Identity;

namespace GenZStore.Models
{
   

    public class ApplicationUser : IdentityUser<Guid>
    {
        public string Role { get; set; } = "Customer"; // Default role is Customer
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<Customer>? Customers { get; set; }
    }
}
