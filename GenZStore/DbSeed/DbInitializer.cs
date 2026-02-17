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
        await context.Database.MigrateAsync();

        // 1. Seed Roles
        string[] roles = { "Owner", "Customer" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        // 2. Seed Admin (Owner)
        var adminEmail = "admin@genzstore.local";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "admin",
                Email = adminEmail,
                Role = "Owner",
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(admin, "Admin@123");
            if (result.Succeeded) await userManager.AddToRoleAsync(admin, "Owner");
        }

        // 3. Seed Test Customer 👈 (For your testing)
        var customerEmail = "kakada@test.local";
        if (await userManager.FindByEmailAsync(customerEmail) == null)
        {
            var customer = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "kakada_dev",
                Email = customerEmail,
                Role = "Customer",
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(customer, "Customer@123");
            if (result.Succeeded) await userManager.AddToRoleAsync(customer, "Customer");
        }

        
    }
}