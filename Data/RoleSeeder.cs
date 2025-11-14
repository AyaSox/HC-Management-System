using HRManagementSystem.Data;
using Microsoft.AspNetCore.Identity;

namespace HRManagementSystem.Data
{
    public static class RoleSeeder
    {
        public static async Task SeedAsync(IServiceProvider sp)
        {
            try
            {
                var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
                var users = sp.GetRequiredService<UserManager<IdentityUser>>();

                // Only create roles if they don't exist
                var rolesToCreate = new[] { "Admin", "HR" };
                foreach (var roleName in rolesToCreate)
                {
                    if (!await roles.RoleExistsAsync(roleName))
                    {
                        await roles.CreateAsync(new IdentityRole(roleName));
                    }
                }

                // Only create admin if doesn't exist
                var adminEmail = "admin@hrsystem.com";
                var admin = await users.FindByEmailAsync(adminEmail);
                if (admin == null)
                {
                    admin = new IdentityUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true
                    };

                    var result = await users.CreateAsync(admin, "Admin@123");
                    if (result.Succeeded)
                    {
                        await users.AddToRoleAsync(admin, "Admin");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the app
                Console.WriteLine($"Role seeding error: {ex.Message}");
            }
        }
    }
}