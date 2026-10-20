using Ecom.Core.Constants;
using Ecom.Core.Entities.User;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ECom.Infrastructure.Data
{
    /// <summary>
    /// Idempotent Identity bootstrap. Ensures the Admin role exists (the catalog write
    /// endpoints require it) and, when <c>Seed:AdminEmail</c> names an existing account,
    /// promotes that account to Admin. Runs once at startup and is safe to run repeatedly;
    /// no default credentials are created.
    /// </summary>
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
        {
            using var scope = services.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync(AppRoles.Admin))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(AppRoles.Admin));
                if (result.Succeeded)
                {
                    logger.LogInformation("Created '{Role}' role.", AppRoles.Admin);
                }
                else
                {
                    logger.LogWarning("Could not create '{Role}' role: {Errors}",
                        AppRoles.Admin,
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }

            var adminEmail = configuration["Seed:AdminEmail"];
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return;
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await userManager.FindByEmailAsync(adminEmail);
            if (user is null)
            {
                logger.LogWarning(
                    "Seed:AdminEmail '{Email}' does not match an existing user; nothing promoted.",
                    adminEmail);
                return;
            }

            if (!await userManager.IsInRoleAsync(user, AppRoles.Admin))
            {
                var result = await userManager.AddToRoleAsync(user, AppRoles.Admin);
                if (result.Succeeded)
                {
                    logger.LogInformation("Promoted '{Email}' to '{Role}'.", adminEmail, AppRoles.Admin);
                }
                else
                {
                    logger.LogWarning("Could not promote '{Email}': {Errors}",
                        adminEmail,
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}
