using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ward.Constants;
using ward.Data;
using Xunit;

namespace ward.Tests
{
    public class DbInitializerTests
    {
        private ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));

            services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            services.AddLogging();
            services.AddScoped<IDbInitializer, DbInitializer>();

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task InitializeAsync_SeedsRoles_AdminUser_AndMasterData()
        {
            var provider = BuildServiceProvider();
            using var scope = provider.CreateScope();

            var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Run initialization
            await initializer.InitializeAsync();

            // 1. Verify 4 Roles exist
            Assert.True(await roleManager.RoleExistsAsync(AppRoles.Admin));
            Assert.True(await roleManager.RoleExistsAsync(AppRoles.Manager));
            Assert.True(await roleManager.RoleExistsAsync(AppRoles.Cashier));
            Assert.True(await roleManager.RoleExistsAsync(AppRoles.InventoryClerk));

            // 2. Verify Development Admin user exists and has Admin role
            var adminUser = await userManager.FindByEmailAsync("admin@flowershop.local");
            Assert.NotNull(adminUser);
            Assert.True(await userManager.IsInRoleAsync(adminUser, AppRoles.Admin));

            // 3. Verify Categories are seeded
            var categories = await context.Categories.ToListAsync();
            Assert.True(categories.Count >= 5);
            Assert.Contains(categories, c => c.Name == "Fresh Cut Roses");

            // 4. Verify Units of Measure are seeded
            var units = await context.UnitsOfMeasure.ToListAsync();
            Assert.True(units.Count >= 5);
            Assert.Contains(units, u => u.Name == "Stem" && u.Symbol == "stm");

            // 5. Verify Warehouses are seeded
            var warehouses = await context.Warehouses.ToListAsync();
            Assert.Equal(2, warehouses.Count);
            Assert.Contains(warehouses, w => w.Code == "MAIN" && w.IsDefault);
            Assert.Contains(warehouses, w => w.Code == "COLD-ROOM" && !w.IsDefault);
        }

        [Fact]
        public async Task InitializeAsync_IsIdempotent_CanRunMultipleTimesWithoutDuplication()
        {
            var provider = BuildServiceProvider();
            using var scope = provider.CreateScope();

            var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Run twice
            await initializer.InitializeAsync();
            await initializer.InitializeAsync();

            var warehouses = await context.Warehouses.ToListAsync();
            Assert.Equal(2, warehouses.Count);

            var units = await context.UnitsOfMeasure.ToListAsync();
            Assert.Equal(5, units.Count);
        }
    }
}
