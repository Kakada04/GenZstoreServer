using GenZStore.Data;
using GenZStore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context,
                                       UserManager<ApplicationUser> userManager,
                                       RoleManager<IdentityRole<Guid>> roleManager)
    {
        // Apply migrations
        await context.Database.MigrateAsync();

        // Seed roles
        if (!await roleManager.RoleExistsAsync("Owner"))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>("Owner"));
        }
        if (!await roleManager.RoleExistsAsync("Customer"))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>("Customer"));
        }

        // Seed admin user
        var adminEmail = "admin@genzstore.local";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "admin",
                Email = adminEmail,
                Role = "Owner",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(user, "Admin@123"); // strong default password
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Owner");
            }
        }
    }
}