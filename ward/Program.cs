using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ward.Data;

namespace ward
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? "Server=(localdb)\\mssqllocaldb;Database=FlowerShopDb;Trusted_Connection=True;MultipleActiveResultSets=true";

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            builder.Services.AddScoped<IDbInitializer, DbInitializer>();
            builder.Services.AddScoped<ward.Services.Barcode.IBarcodeService, ward.Services.Barcode.BarcodeService>();
            builder.Services.AddScoped<ward.Services.Catalog.ICategoryService, ward.Services.Catalog.CategoryService>();
            builder.Services.AddScoped<ward.Services.Catalog.IProductService, ward.Services.Catalog.ProductService>();
            builder.Services.AddScoped<ward.Services.Inventory.IWarehouseService, ward.Services.Inventory.WarehouseService>();
            builder.Services.AddScoped<ward.Services.Inventory.IInventoryService, ward.Services.Inventory.InventoryService>();
            builder.Services.AddScoped<ward.Services.Production.IBouquetService, ward.Services.Production.BouquetService>();
            builder.Services.AddScoped<ward.Services.Purchasing.IPurchaseService, ward.Services.Purchasing.PurchaseService>();
            builder.Services.AddScoped<ward.Services.Sales.ISalesService, ward.Services.Sales.SalesService>();
            builder.Services.AddScoped<ward.Services.Inventory.IStocktakingService, ward.Services.Inventory.StocktakingService>();
            builder.Services.AddScoped<ward.Services.Reports.IReportingService, ward.Services.Reports.ReportingService>();

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(ward.Constants.AppPolicies.RequireAdmin, policy =>
                    policy.RequireRole(ward.Constants.AppRoles.Admin));

                options.AddPolicy(ward.Constants.AppPolicies.ManageCatalog, policy =>
                    policy.RequireRole(ward.Constants.AppRoles.Admin, ward.Constants.AppRoles.Manager));

                options.AddPolicy(ward.Constants.AppPolicies.ManageInventory, policy =>
                    policy.RequireRole(ward.Constants.AppRoles.Admin, ward.Constants.AppRoles.Manager, ward.Constants.AppRoles.InventoryClerk));

                options.AddPolicy(ward.Constants.AppPolicies.ProcessSales, policy =>
                    policy.RequireRole(ward.Constants.AppRoles.Admin, ward.Constants.AppRoles.Manager, ward.Constants.AppRoles.Cashier));

                options.AddPolicy(ward.Constants.AppPolicies.ProcessReturns, policy =>
                    policy.RequireRole(ward.Constants.AppRoles.Admin, ward.Constants.AppRoles.Manager));

                options.AddPolicy(ward.Constants.AppPolicies.ViewAuditAndReports, policy =>
                    policy.RequireRole(ward.Constants.AppRoles.Admin, ward.Constants.AppRoles.Manager));

                options.AddPolicy(ward.Constants.AppPolicies.ApproveStocktaking, policy =>
                    policy.RequireRole(ward.Constants.AppRoles.Admin, ward.Constants.AppRoles.Manager));
            });

            builder.Services.AddControllersWithViews()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                });

            var app = builder.Build();

            // Always run migrations and seed (works for both dev and production)
            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
                    initializer.InitializeAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred during database initialization/seeding.");
                }
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

            // Note: HTTPS redirect disabled — MonsterASP.NET/IIS handles SSL at the hosting layer
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
