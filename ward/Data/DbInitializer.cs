using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ward.Constants;
using ward.Entities.Catalog;
using ward.Entities.Inventory;

namespace ward.Data
{
    public interface IDbInitializer
    {
        Task InitializeAsync();
    }

    public class DbInitializer : IDbInitializer
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public DbInitializer(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task InitializeAsync()
        {
            // Apply pending migrations in development if relational database provider
            if (_context.Database.IsRelational())
            {
                await _context.Database.MigrateAsync();
            }

            // 1. Seed Roles
            string[] roles = { AppRoles.Admin, AppRoles.Manager, AppRoles.Cashier, AppRoles.InventoryClerk };
            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Seed Default Development Admin
            var defaultAdminEmail = "admin@flowershop.local";
            var existingAdmin = await _userManager.FindByEmailAsync(defaultAdminEmail);
            if (existingAdmin == null)
            {
                var adminUser = new IdentityUser
                {
                    UserName = defaultAdminEmail,
                    Email = defaultAdminEmail,
                    EmailConfirmed = true
                };

                // Development seed password adhering to password requirements
                var result = await _userManager.CreateAsync(adminUser, "Admin@123456");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(adminUser, AppRoles.Admin);
                }
            }

            // 3. Seed Units of Measure
            if (!await _context.UnitsOfMeasure.AnyAsync())
            {
                _context.UnitsOfMeasure.AddRange(
                    new UnitOfMeasure { Name = "Stem", Symbol = "stm", AllowDecimals = false },
                    new UnitOfMeasure { Name = "Bunch", Symbol = "bn", AllowDecimals = false },
                    new UnitOfMeasure { Name = "Piece", Symbol = "pc", AllowDecimals = false },
                    new UnitOfMeasure { Name = "Meter", Symbol = "m", AllowDecimals = true },
                    new UnitOfMeasure { Name = "Box", Symbol = "bx", AllowDecimals = false }
                );
                await _context.SaveChangesAsync();
            }

            // 4. Seed Product Categories
            if (!await _context.Categories.AnyAsync())
            {
                _context.Categories.AddRange(
                    new Category { Name = "Fresh Cut Roses", Description = "Premium long stem and spray roses", IsActive = true },
                    new Category { Name = "Lilies & Orchids", Description = "Exotic lilies and potted/cut orchids", IsActive = true },
                    new Category { Name = "Foliage & Fillers", Description = "Eucalyptus, gypsophila, and greenery", IsActive = true },
                    new Category { Name = "Hand-Tied Bouquets", Description = "Artisan and made-to-order floral bouquets", IsActive = true },
                    new Category { Name = "Vases & Packaging", Description = "Ceramic pots, glass vases, wraps, ribbons", IsActive = true }
                );
                await _context.SaveChangesAsync();
            }

            // 5. Seed Warehouses
            if (!await _context.Warehouses.AnyAsync())
            {
                _context.Warehouses.AddRange(
                    new Warehouse
                    {
                        Code = "MAIN",
                        Name = "Main Store Floor",
                        Location = "Ground Floor Display Area",
                        IsDefault = true,
                        IsActive = true
                    },
                    new Warehouse
                    {
                        Code = "COLD-ROOM",
                        Name = "Cold Storage Facility",
                        Location = "Rear Temperature Controlled Room (4°C)",
                        IsDefault = false,
                        IsActive = true
                    }
                );
                await _context.SaveChangesAsync();
            }

            // 6. Seed Default Suppliers
            if (!await _context.Suppliers.AnyAsync())
            {
                _context.Suppliers.AddRange(
                    new ward.Entities.Purchasing.Supplier
                    {
                        Name = "Flora Wholesale Holland",
                        ContactPerson = "Jan de Boer",
                        Phone = "+31 20 555 1234",
                        Email = "orders@floraholland.example.com",
                        TaxNumber = "NL889911223B01",
                        Address = "Aalsmeer Flower Auction, Bloemenveiling 1, Netherlands",
                        IsActive = true
                    },
                    new ward.Entities.Purchasing.Supplier
                    {
                        Name = "Bloom Packaging & Ribbons",
                        ContactPerson = "Sarah Miller",
                        Phone = "+1 555 234 5678",
                        Email = "sales@bloompack.example.com",
                        TaxNumber = "US994433221",
                        Address = "742 Evergreen Terrace, Industrial Park, Oregon",
                        IsActive = true
                    },
                    new ward.Entities.Purchasing.Supplier
                    {
                        Name = "Al-Riyadh Flower Farms & Greenery",
                        ContactPerson = "Tariq Al-Mansoor",
                        Phone = "+966 11 444 7788",
                        Email = "supply@riyadhfarms.example.com",
                        TaxNumber = "SA300123456700003",
                        Address = "Diriyah Agricultural Sector, Riyadh, Saudi Arabia",
                        IsActive = true
                    }
                );
                await _context.SaveChangesAsync();
            }
        }
    }
}
