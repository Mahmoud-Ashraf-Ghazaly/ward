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
            });

            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // Seed database during development
            if (app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();
                var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
                initializer.InitializeAsync().GetAwaiter().GetResult();
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

            app.UseHttpsRedirection();
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
