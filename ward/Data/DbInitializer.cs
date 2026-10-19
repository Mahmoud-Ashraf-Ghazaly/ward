using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ward.Constants;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Entities.Inventory;
using ward.Entities.Production;
using ward.Entities.Purchasing;
using ward.Entities.Sales;

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
            if (_context.Database.IsRelational())
            {
                await _context.Database.MigrateAsync();
            }

            // 1. Roles & Users
            await SeedRolesAndUsersAsync();

            // 2. Units of Measure
            await SeedUnitsOfMeasureAsync();

            // 3. Categories
            await SeedCategoriesAsync();

            // 4. Warehouses
            await SeedWarehousesAsync();

            // 5. Suppliers
            await SeedSuppliersAsync();

            // 6. Customers
            await SeedCustomersAsync();

            // 7. Products & Bouquet Recipes
            await SeedProductsAndRecipesAsync();

            // 8. Stock Items
            await SeedStockItemsAsync();

            // 9. Purchasing (Invoices, Items, Returns, Payments)
            await SeedPurchasingAsync();

            // 10. Sales (Invoices, Items, Component Consumptions, Payments, Returns)
            await SeedSalesAsync();

            // 11. Stocktaking (Audits, Lines, Variances)
            await SeedStocktakingAsync();

            // 12. Stock Movements Audit Trail
            await SeedStockMovementsAsync();
        }

        private async Task SeedRolesAndUsersAsync()
        {
            string[] roles = { AppRoles.Admin, AppRoles.Manager, AppRoles.Cashier, AppRoles.InventoryClerk };
            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var defaultUsers = new[]
            {
                (Email: "admin@flowershop.local", Role: AppRoles.Admin),
                (Email: "manager@flowershop.local", Role: AppRoles.Manager),
                (Email: "cashier@flowershop.local", Role: AppRoles.Cashier),
                (Email: "inventory@flowershop.local", Role: AppRoles.InventoryClerk)
            };

            foreach (var userDef in defaultUsers)
            {
                var existingUser = await _userManager.FindByEmailAsync(userDef.Email);
                if (existingUser == null)
                {
                    var user = new IdentityUser
                    {
                        UserName = userDef.Email,
                        Email = userDef.Email,
                        EmailConfirmed = true
                    };

                    var result = await _userManager.CreateAsync(user, "Admin@123456");
                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, userDef.Role);
                    }
                }
            }
        }

        private async Task SeedUnitsOfMeasureAsync()
        {
            var units = new[]
            {
                new UnitOfMeasure { Name = "Stem", Symbol = "stm", AllowDecimals = false },
                new UnitOfMeasure { Name = "Bunch", Symbol = "bn", AllowDecimals = false },
                new UnitOfMeasure { Name = "Piece", Symbol = "pc", AllowDecimals = false },
                new UnitOfMeasure { Name = "Meter", Symbol = "m", AllowDecimals = true },
                new UnitOfMeasure { Name = "Box", Symbol = "bx", AllowDecimals = false }
            };

            foreach (var u in units)
            {
                if (!await _context.UnitsOfMeasure.AnyAsync(existing => existing.Name == u.Name))
                {
                    _context.UnitsOfMeasure.Add(u);
                }
            }
            await _context.SaveChangesAsync();
        }

        private async Task SeedCategoriesAsync()
        {
            var categories = new[]
            {
                new Category { Name = "Fresh Cut Roses", Description = "Premium long stem and spray roses", IsActive = true },
                new Category { Name = "Lilies & Orchids", Description = "Exotic lilies and potted/cut orchids", IsActive = true },
                new Category { Name = "Foliage & Fillers", Description = "Eucalyptus, gypsophila, and greenery", IsActive = true },
                new Category { Name = "Exotic & Seasonal Blooms", Description = "Seasonal and imported blooms", IsActive = true },
                new Category { Name = "Hand-Tied Bouquets", Description = "Artisan and made-to-order floral bouquets", IsActive = true },
                new Category { Name = "Luxury Hat Boxes", Description = "Signature hat box arrangements", IsActive = true },
                new Category { Name = "Vases & Packaging", Description = "Ceramic pots, glass vases, wraps, ribbons", IsActive = true },
                new Category { Name = "Vases & Glassware", Description = "Premium glassware and ceramic vases", IsActive = true },
                new Category { Name = "Wrapping & Ribbons", Description = "Florist wraps and luxury ribbons", IsActive = true },
                new Category { Name = "Accessories & Cards", Description = "Greeting cards and gift accessories", IsActive = true },
                new Category { Name = "Artificial & Dried Flowers", Description = "Preserved and everlasting florals", IsActive = true },
                new Category { Name = "Event & Styling Services", Description = "Custom arrangement and event services", IsActive = true }
            };

            foreach (var cat in categories)
            {
                if (!await _context.Categories.AnyAsync(existing => existing.Name == cat.Name))
                {
                    _context.Categories.Add(cat);
                }
            }
            await _context.SaveChangesAsync();
        }

        private async Task SeedWarehousesAsync()
        {
            var warehouses = new[]
            {
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
                },
                new Warehouse
                {
                    Code = "PKG-ROOM",
                    Name = "Packaging & Supplies Room",
                    Location = "Mezzanine Floor Room 2",
                    IsDefault = false,
                    IsActive = true
                }
            };

            foreach (var w in warehouses)
            {
                if (!await _context.Warehouses.AnyAsync(existing => existing.Code == w.Code))
                {
                    _context.Warehouses.Add(w);
                }
            }
            await _context.SaveChangesAsync();
        }

        private async Task SeedSuppliersAsync()
        {
            var suppliers = new[]
            {
                new Supplier
                {
                    Name = "Flora Wholesale Holland",
                    ContactPerson = "Jan de Boer",
                    Phone = "+31 20 555 1234",
                    Email = "orders@floraholland.example.com",
                    TaxNumber = "NL889911223B01",
                    Address = "Aalsmeer Flower Auction, Bloemenveiling 1, Netherlands",
                    CurrentBalance = 1450.00m,
                    IsActive = true
                },
                new Supplier
                {
                    Name = "Bloom Packaging & Ribbons",
                    ContactPerson = "Sarah Miller",
                    Phone = "+1 555 234 5678",
                    Email = "sales@bloompack.example.com",
                    TaxNumber = "US994433221",
                    Address = "742 Evergreen Terrace, Industrial Park, Oregon",
                    CurrentBalance = 320.50m,
                    IsActive = true
                },
                new Supplier
                {
                    Name = "Al-Riyadh Flower Farms & Greenery",
                    ContactPerson = "Tariq Al-Mansoor",
                    Phone = "+966 11 444 7788",
                    Email = "supply@riyadhfarms.example.com",
                    TaxNumber = "SA300123456700003",
                    Address = "Diriyah Agricultural Sector, Riyadh, Saudi Arabia",
                    CurrentBalance = 2100.00m,
                    IsActive = true
                },
                new Supplier
                {
                    Name = "Medellin Rose Plantation Ltd.",
                    ContactPerson = "Carlos Hernandez",
                    Phone = "+57 4 332 9988",
                    Email = "exports@medellinroses.example.com",
                    TaxNumber = "CO900124578-1",
                    Address = "Rionegro Flower Valley, Antioquia, Colombia",
                    CurrentBalance = 0.00m,
                    IsActive = true
                },
                new Supplier
                {
                    Name = "Tuscany Glass & Ceramic Works",
                    ContactPerson = "Matteo Rossi",
                    Phone = "+39 055 889 441",
                    Email = "info@tuscanyceramics.example.com",
                    TaxNumber = "IT01294820485",
                    Address = "Via delle Fornaci 14, Florence, Italy",
                    CurrentBalance = 780.00m,
                    IsActive = true
                },
                new Supplier
                {
                    Name = "Zenith Floral Supplies & Oasis Co.",
                    ContactPerson = "Lin Wei",
                    Phone = "+86 20 8123 4567",
                    Email = "inquiries@zenithfloral.example.com",
                    TaxNumber = "CN91440101MA59",
                    Address = "Baiyun Industrial Zone, Guangzhou, China",
                    CurrentBalance = 450.00m,
                    IsActive = true
                }
            };

            foreach (var s in suppliers)
            {
                var existing = await _context.Suppliers.FirstOrDefaultAsync(x => x.Name == s.Name);
                if (existing == null)
                {
                    _context.Suppliers.Add(s);
                }
                else
                {
                    existing.CurrentBalance = s.CurrentBalance;
                }
            }
            await _context.SaveChangesAsync();
        }

        private async Task SeedCustomersAsync()
        {
            var customers = new[]
            {
                new Customer
                {
                    Name = "Walk-in Retail Customer",
                    Phone = "555-0100",
                    Email = "walkin@flowershop.local",
                    IsActive = true
                },
                new Customer
                {
                    Name = "Ritz Royal Events & Weddings",
                    Phone = "+1 555 789 0123",
                    Email = "weddings@ritzroyal.example.com",
                    Address = "500 Grand Avenue, Downtown",
                    IsActive = true
                },
                new Customer
                {
                    Name = "Grand Hyatt Hotel Ballroom Decor",
                    Phone = "+1 555 987 6543",
                    Email = "decor@grandhyatt.example.com",
                    Address = "100 Luxury Boulevard, Financial District",
                    IsActive = true
                },
                new Customer
                {
                    Name = "Sophia Al-Hashimi",
                    Phone = "+966 50 123 4567",
                    Email = "sophia.hashimi@example.com",
                    Address = "Al-Nakheel Villas, Villa 12, Riyadh",
                    IsActive = true
                },
                new Customer
                {
                    Name = "David Miller & Sarah Jenkins Wedding",
                    Phone = "+1 555 456 7890",
                    Email = "miller.jenkins2026@example.com",
                    Address = "24 Lakeside Drive, Westlake",
                    IsActive = true
                },
                new Customer
                {
                    Name = "Boutique Bloom Café & Bakery",
                    Phone = "+1 555 333 4444",
                    Email = "management@bloomcafe.example.com",
                    Address = "88 Arts Quarter, Market Square",
                    IsActive = true
                },
                new Customer
                {
                    Name = "Apex Financial Corp Corporate Reception",
                    Phone = "+1 555 888 9999",
                    Email = "facilities@apexfinancial.example.com",
                    Address = "Tower 1, 42nd Floor, Wall Street Center",
                    IsActive = true
                },
                new Customer
                {
                    Name = "Emerald Charity Gala 2026",
                    Phone = "+1 555 222 1111",
                    Email = "events@emeraldgala.example.com",
                    Address = "Convention Center Hall B, Metropolis",
                    IsActive = true
                },
                new Customer
                {
                    Name = "Michael Vance",
                    Phone = "+1 555 654 3210",
                    Email = "mvance@example.com",
                    Address = "14 Pine Street, Apt 3B",
                    IsActive = true
                },
                new Customer
                {
                    Name = "Nora Al-Qahtani",
                    Phone = "+966 55 987 6543",
                    Email = "nora.qahtani@example.com",
                    Address = "Al-Olaya District, Riyadh",
                    IsActive = true
                }
            };

            foreach (var c in customers)
            {
                if (!await _context.Customers.AnyAsync(existing => existing.Name == c.Name))
                {
                    _context.Customers.Add(c);
                }
            }
            await _context.SaveChangesAsync();
        }

        private async Task SeedProductsAndRecipesAsync()
        {
            if (await _context.Products.CountAsync() >= 25)
            {
                return;
            }

            var catRoses = await _context.Categories.FirstAsync(c => c.Name == "Fresh Cut Roses");
            var catLilies = await _context.Categories.FirstAsync(c => c.Name == "Lilies & Orchids");
            var catFoliage = await _context.Categories.FirstAsync(c => c.Name == "Foliage & Fillers");
            var catSeasonal = await _context.Categories.FirstAsync(c => c.Name == "Exotic & Seasonal Blooms");
            var catBouquets = await _context.Categories.FirstAsync(c => c.Name == "Hand-Tied Bouquets");
            var catHatBoxes = await _context.Categories.FirstAsync(c => c.Name == "Luxury Hat Boxes");
            var catVases = await _context.Categories.FirstAsync(c => c.Name == "Vases & Glassware");
            var catWrapping = await _context.Categories.FirstAsync(c => c.Name == "Wrapping & Ribbons");
            var catAccessories = await _context.Categories.FirstAsync(c => c.Name == "Accessories & Cards");
            var catArtificial = await _context.Categories.FirstAsync(c => c.Name == "Artificial & Dried Flowers");
            var catServices = await _context.Categories.FirstAsync(c => c.Name == "Event & Styling Services");

            var uomStem = await _context.UnitsOfMeasure.FirstAsync(u => u.Name == "Stem");
            var uomBunch = await _context.UnitsOfMeasure.FirstAsync(u => u.Name == "Bunch");
            var uomPiece = await _context.UnitsOfMeasure.FirstAsync(u => u.Name == "Piece");
            var uomMeter = await _context.UnitsOfMeasure.FirstAsync(u => u.Name == "Meter");
            var uomBox = await _context.UnitsOfMeasure.FirstAsync(u => u.Name == "Box");

            var products = new List<Product>
            {
                // 1. Fresh Flowers (Stems & Bunches)
                new Product
                {
                    SKU = "FLW-ROS-RED",
                    Barcode = "628100100001",
                    Name = "Red Naomi Dutch Rose (70cm)",
                    Description = "Velvety deep-red premium long-stem Dutch rose",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catRoses.Id,
                    UnitOfMeasureId = uomStem.Id,
                    UnitPrice = 4.50m,
                    CurrentAverageCost = 1.80m,
                    MinimumStockLevel = 50,
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-ROS-WHT",
                    Barcode = "628100100002",
                    Name = "Avalanche White Rose (60cm)",
                    Description = "Crisp ivory white rose with lush petal count",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catRoses.Id,
                    UnitOfMeasureId = uomStem.Id,
                    UnitPrice = 4.50m,
                    CurrentAverageCost = 1.80m,
                    MinimumStockLevel = 40,
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-ROS-PNK",
                    Barcode = "628100100003",
                    Name = "Sweet Dolomiti Pink Rose",
                    Description = "Gentle pastel blush pink garden-style rose",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catRoses.Id,
                    UnitOfMeasureId = uomStem.Id,
                    UnitPrice = 4.00m,
                    CurrentAverageCost = 1.60m,
                    MinimumStockLevel = 35,
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-LIL-ORNT",
                    Barcode = "628100100004",
                    Name = "Oriental Casablanca White Lily",
                    Description = "Fragrant multi-bloom pure white oriental lily",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catLilies.Id,
                    UnitOfMeasureId = uomStem.Id,
                    UnitPrice = 8.00m,
                    CurrentAverageCost = 3.50m,
                    MinimumStockLevel = 25,
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-ORC-DEN",
                    Barcode = "628100100005",
                    Name = "Dendrobium Violet Orchid Spray",
                    Description = "Vibrant exotic purple dendrobium orchid stem",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catLilies.Id,
                    UnitOfMeasureId = uomStem.Id,
                    UnitPrice = 6.50m,
                    CurrentAverageCost = 2.80m,
                    MinimumStockLevel = 20,
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-TUL-YEL",
                    Barcode = "628100100006",
                    Name = "Strong Gold Dutch Tulips (10 Stems)",
                    Description = "Bright canary yellow Dutch tulips bundle",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catSeasonal.Id,
                    UnitOfMeasureId = uomBunch.Id,
                    UnitPrice = 18.00m,
                    CurrentAverageCost = 8.50m,
                    MinimumStockLevel = 15,
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-HYD-BLU",
                    Barcode = "628100100007",
                    Name = "Dutch Royal Blue Hydrangea",
                    Description = "Giant head fresh blue hydrangea stem",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catSeasonal.Id,
                    UnitOfMeasureId = uomStem.Id,
                    UnitPrice = 12.00m,
                    CurrentAverageCost = 5.00m,
                    MinimumStockLevel = 15,
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-SNF-GLD",
                    Barcode = "628100100008",
                    Name = "Vincent Choice Sunflower",
                    Description = "Pollen-free radiant golden sunflower stem",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catSeasonal.Id,
                    UnitOfMeasureId = uomStem.Id,
                    UnitPrice = 5.00m,
                    CurrentAverageCost = 2.00m,
                    MinimumStockLevel = 20,
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-PEO-PNK",
                    Barcode = "628100100014",
                    Name = "Sarah Bernhardt Pink Peony",
                    Description = "Luxury double pink French peony stem (Seasonal)",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catSeasonal.Id,
                    UnitOfMeasureId = uomStem.Id,
                    UnitPrice = 15.00m,
                    CurrentAverageCost = 7.00m,
                    MinimumStockLevel = 10, // Will be set to LOW STOCK in inventory
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-GYP-WHT",
                    Barcode = "628100100009",
                    Name = "Million Stars Gypsophila (Baby's Breath)",
                    Description = "Full volume fluffy white gypsophila bunch",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catFoliage.Id,
                    UnitOfMeasureId = uomBunch.Id,
                    UnitPrice = 14.00m,
                    CurrentAverageCost = 5.50m,
                    MinimumStockLevel = 25,
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-EUC-CIN",
                    Barcode = "628100100010",
                    Name = "Silver Dollar Eucalyptus Greenery",
                    Description = "Aromatic rounded silvery eucalyptus foliage bunch",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catFoliage.Id,
                    UnitOfMeasureId = uomBunch.Id,
                    UnitPrice = 10.00m,
                    CurrentAverageCost = 3.80m,
                    MinimumStockLevel = 30,
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-RUS-ITA",
                    Barcode = "628100100011",
                    Name = "Italian Ruscus Greenery",
                    Description = "Trailing glossy green foliage bunch for bouquets",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catFoliage.Id,
                    UnitOfMeasureId = uomBunch.Id,
                    UnitPrice = 9.00m,
                    CurrentAverageCost = 3.20m,
                    MinimumStockLevel = 20,
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-CHR-YEL",
                    Barcode = "628100100013",
                    Name = "Yellow Anastasia Spider Chrysanthemum",
                    Description = "Exotic spider chrysanthemum single head",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catSeasonal.Id,
                    UnitOfMeasureId = uomStem.Id,
                    UnitPrice = 3.50m,
                    CurrentAverageCost = 1.20m,
                    MinimumStockLevel = 30,
                    IsActive = true
                },

                // 2. Artificial & Preserved
                new Product
                {
                    SKU = "ART-ROS-VEL",
                    Barcode = "628100200001",
                    Name = "Velvet Touch Silk Red Rose",
                    Description = "Ultra-realistic faux silk red rose with wired stem",
                    ProductType = ProductType.ArtificialFlower,
                    CategoryId = catArtificial.Id,
                    UnitOfMeasureId = uomStem.Id,
                    UnitPrice = 6.00m,
                    CurrentAverageCost = 2.20m,
                    MinimumStockLevel = 20,
                    IsActive = true
                },
                new Product
                {
                    SKU = "ART-LAV-BUN",
                    Barcode = "628100200003",
                    Name = "Dried French Lavender Bundle",
                    Description = "Naturally dried fragrant Provence lavender bundle",
                    ProductType = ProductType.ArtificialFlower,
                    CategoryId = catArtificial.Id,
                    UnitOfMeasureId = uomBunch.Id,
                    UnitPrice = 16.00m,
                    CurrentAverageCost = 6.00m,
                    MinimumStockLevel = 12,
                    IsActive = true
                },
                new Product
                {
                    SKU = "ART-PAM-GRS",
                    Barcode = "628100200004",
                    Name = "Fluffy Natural Pampas Grass Stems",
                    Description = "110cm dried beige pampas plume stem",
                    ProductType = ProductType.ArtificialFlower,
                    CategoryId = catArtificial.Id,
                    UnitOfMeasureId = uomStem.Id,
                    UnitPrice = 7.50m,
                    CurrentAverageCost = 2.80m,
                    MinimumStockLevel = 15,
                    IsActive = true
                },

                // 3. Vases & Planters
                new Product
                {
                    SKU = "VAS-GLS-CYL",
                    Barcode = "628100300001",
                    Name = "Clear Cylinder Glass Vase 30cm",
                    Description = "Heavy base crystalline cylinder glass vase",
                    ProductType = ProductType.Vase,
                    CategoryId = catVases.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 14.00m,
                    CurrentAverageCost = 5.00m,
                    MinimumStockLevel = 15,
                    IsActive = true
                },
                new Product
                {
                    SKU = "VAS-CER-RND",
                    Barcode = "628100300002",
                    Name = "Matte White Ribbed Ceramic Planter",
                    Description = "Minimalist Nordic ceramic ribbed pot",
                    ProductType = ProductType.Vase,
                    CategoryId = catVases.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 22.00m,
                    CurrentAverageCost = 9.00m,
                    MinimumStockLevel = 10,
                    IsActive = true
                },
                new Product
                {
                    SKU = "VAS-GLS-FLT",
                    Barcode = "628100300003",
                    Name = "Fluted Emerald Green Crystal Vase",
                    Description = "Vintage art deco emerald fluted vase",
                    ProductType = ProductType.Vase,
                    CategoryId = catVases.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 28.00m,
                    CurrentAverageCost = 11.50m,
                    MinimumStockLevel = 8,
                    IsActive = true
                },
                new Product
                {
                    SKU = "VAS-MET-GLD",
                    Barcode = "628100300004",
                    Name = "Brushed Gold Geometric Metal Urn",
                    Description = "Luxury centerpiece metallic floral pedestal urn",
                    ProductType = ProductType.Vase,
                    CategoryId = catVases.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 38.00m,
                    CurrentAverageCost = 16.00m,
                    MinimumStockLevel = 6, // Will be set to 0 stock (OUT OF STOCK test)
                    IsActive = true
                },

                // 4. Wrapping Materials
                new Product
                {
                    SKU = "WRP-KRF-PPR",
                    Barcode = "628100400001",
                    Name = "Natural Eco Kraft Flower Wrapping Paper",
                    Description = "Heavyweight 80gsm natural ribbed kraft sheets",
                    ProductType = ProductType.WrappingMaterial,
                    CategoryId = catWrapping.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 2.00m,
                    CurrentAverageCost = 0.60m,
                    MinimumStockLevel = 50,
                    IsActive = true
                },
                new Product
                {
                    SKU = "WRP-MAT-KOR",
                    Barcode = "628100400002",
                    Name = "Korean Waterproof Matte Pastel Wrap Sheet",
                    Description = "Silky frost finish waterproof dual-sided wrapping sheet",
                    ProductType = ProductType.WrappingMaterial,
                    CategoryId = catWrapping.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 3.00m,
                    CurrentAverageCost = 0.90m,
                    MinimumStockLevel = 60,
                    IsActive = true
                },
                new Product
                {
                    SKU = "WRP-RIB-SAT",
                    Barcode = "628100400003",
                    Name = "Double-Faced Silk Satin Ribbon (38mm)",
                    Description = "High luster luxury satin ribbon sold by meter",
                    ProductType = ProductType.WrappingMaterial,
                    CategoryId = catWrapping.Id,
                    UnitOfMeasureId = uomMeter.Id, // Allows decimals!
                    UnitPrice = 1.50m,
                    CurrentAverageCost = 0.40m,
                    MinimumStockLevel = 100,
                    IsActive = true
                },
                new Product
                {
                    SKU = "WRP-BOX-CYL",
                    Barcode = "628100400005",
                    Name = "Luxury Velvet Hat Box with Gold Rim",
                    Description = "Rigid circular presentation hat box with satin rope",
                    ProductType = ProductType.WrappingMaterial,
                    CategoryId = catHatBoxes.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 12.00m,
                    CurrentAverageCost = 4.50m,
                    MinimumStockLevel = 20,
                    IsActive = true
                },

                // 5. Accessories
                new Product
                {
                    SKU = "ACC-CRD-LUX",
                    Barcode = "628100500001",
                    Name = "Embossed Botanical Gold-Foil Greeting Card",
                    Description = "Luxury 350gsm hot stamped card with envelope",
                    ProductType = ProductType.Accessory,
                    CategoryId = catAccessories.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 3.50m,
                    CurrentAverageCost = 0.80m,
                    MinimumStockLevel = 40,
                    IsActive = true
                },
                new Product
                {
                    SKU = "ACC-TOY-BR",
                    Barcode = "628100500002",
                    Name = "Plush Keepsake Teddy Bear with Satin Bow",
                    Description = "25cm cuddly teddy bear gift accompaniment",
                    ProductType = ProductType.Accessory,
                    CategoryId = catAccessories.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 18.00m,
                    CurrentAverageCost = 6.50m,
                    MinimumStockLevel = 15,
                    IsActive = true
                },
                new Product
                {
                    SKU = "ACC-OAS-BLK",
                    Barcode = "628100500004",
                    Name = "Oasis Maxlife Floral Foam Brick",
                    Description = "Water-absorbing density foam brick for arrangements",
                    ProductType = ProductType.Accessory,
                    CategoryId = catAccessories.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 3.00m,
                    CurrentAverageCost = 1.10m,
                    MinimumStockLevel = 30,
                    IsActive = true
                },
                new Product
                {
                    SKU = "ACC-FLR-FOD",
                    Barcode = "628100500003",
                    Name = "Chrysal Universal Flower Food (50 Pack)",
                    Description = "Nutrient sachet box extending bloom freshness",
                    ProductType = ProductType.Accessory,
                    CategoryId = catAccessories.Id,
                    UnitOfMeasureId = uomBox.Id,
                    UnitPrice = 15.00m,
                    CurrentAverageCost = 6.00m,
                    MinimumStockLevel = 10,
                    IsActive = true
                },

                // 6. Services
                new Product
                {
                    SKU = "SRV-DEL-EXP",
                    Barcode = null,
                    Name = "Express Temperature-Controlled Delivery",
                    Description = "Same-day priority refrigerated floral delivery",
                    ProductType = ProductType.Service,
                    CategoryId = catServices.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 15.00m,
                    CurrentAverageCost = 5.00m,
                    MinimumStockLevel = 0,
                    IsActive = true
                },
                new Product
                {
                    SKU = "SRV-EVT-STP",
                    Barcode = null,
                    Name = "On-Site Event Floral Styling & Installation",
                    Description = "Lead florist setup, staging, and breakdown service",
                    ProductType = ProductType.Service,
                    CategoryId = catServices.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 120.00m,
                    CurrentAverageCost = 40.00m,
                    MinimumStockLevel = 0,
                    IsActive = true
                },

                // 7. Made-to-order Artisan Bouquets
                new Product
                {
                    SKU = "BQT-ROSE-24",
                    Barcode = "628100600001",
                    Name = "Royal Red Passion 24 Roses Deluxe",
                    Description = "24 Red Naomi roses hand-tied with eucalyptus, gypsophila, and Korean pastel wrap",
                    ProductType = ProductType.Bouquet,
                    CategoryId = catBouquets.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 145.00m,
                    CurrentAverageCost = 62.50m,
                    MinimumStockLevel = 5,
                    IsActive = true
                },
                new Product
                {
                    SKU = "BQT-WHT-PUR",
                    Barcode = "628100600002",
                    Name = "Pure Casablanca Lily & Rose Symphony",
                    Description = "12 Avalanche white roses with 3 oriental lilies and Italian ruscus in natural kraft",
                    ProductType = ProductType.Bouquet,
                    CategoryId = catBouquets.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 110.00m,
                    CurrentAverageCost = 46.20m,
                    MinimumStockLevel = 5,
                    IsActive = true
                },
                new Product
                {
                    SKU = "BQT-SUN-SUM",
                    Barcode = "628100600003",
                    Name = "Golden Sunshine Meadow Hand-Tied",
                    Description = "5 Sunflowers, 8 spider chrysanthemums, baby's breath, and rustic kraft wrap",
                    ProductType = ProductType.Bouquet,
                    CategoryId = catBouquets.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 75.00m,
                    CurrentAverageCost = 31.80m,
                    MinimumStockLevel = 8,
                    IsActive = true
                },
                new Product
                {
                    SKU = "BQT-BOX-ROM",
                    Barcode = "628100600005",
                    Name = "Velvet Romance Pink Hat Box Arrangement",
                    Description = "15 Pink roses with baby's breath arranged inside a luxury velvet hat box",
                    ProductType = ProductType.Bouquet,
                    CategoryId = catHatBoxes.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 135.00m,
                    CurrentAverageCost = 55.40m,
                    MinimumStockLevel = 6,
                    IsActive = true
                },
                new Product
                {
                    SKU = "BQT-ORC-LUX",
                    Barcode = "628100600004",
                    Name = "Exotic Violet Orchid & Hydrangea Grand Vase",
                    Description = "6 Violet orchids with 3 blue hydrangeas and eucalyptus styled in a cylinder vase",
                    ProductType = ProductType.Bouquet,
                    CategoryId = catVases.Id,
                    UnitOfMeasureId = uomPiece.Id,
                    UnitPrice = 165.00m,
                    CurrentAverageCost = 71.20m,
                    MinimumStockLevel = 4,
                    IsActive = true
                },
                new Product
                {
                    SKU = "FLW-DISC-OLD",
                    Barcode = "628100100099",
                    Name = "Discontinued Autumn Marigold Bunches",
                    Description = "Archived seasonal product for testing inactive filters",
                    ProductType = ProductType.FreshFlower,
                    CategoryId = catSeasonal.Id,
                    UnitOfMeasureId = uomBunch.Id,
                    UnitPrice = 10.00m,
                    CurrentAverageCost = 4.00m,
                    MinimumStockLevel = 0,
                    IsActive = false // Inactive product scenario
                }
            };

            foreach (var p in products)
            {
                var existing = await _context.Products.FirstOrDefaultAsync(x => x.SKU == p.SKU);
                if (existing == null)
                {
                    _context.Products.Add(p);
                }
            }
            await _context.SaveChangesAsync();

            // Link Bouquet Recipes
            await SeedRecipesForBouquetsAsync();
        }

        private async Task SeedRecipesForBouquetsAsync()
        {
            var pRedRose = await _context.Products.FirstAsync(p => p.SKU == "FLW-ROS-RED");
            var pWhtRose = await _context.Products.FirstAsync(p => p.SKU == "FLW-ROS-WHT");
            var pPnkRose = await _context.Products.FirstAsync(p => p.SKU == "FLW-ROS-PNK");
            var pLily = await _context.Products.FirstAsync(p => p.SKU == "FLW-LIL-ORNT");
            var pOrchid = await _context.Products.FirstAsync(p => p.SKU == "FLW-ORC-DEN");
            var pHydrangea = await _context.Products.FirstAsync(p => p.SKU == "FLW-HYD-BLU");
            var pSunflower = await _context.Products.FirstAsync(p => p.SKU == "FLW-SNF-GLD");
            var pChrysan = await _context.Products.FirstAsync(p => p.SKU == "FLW-CHR-YEL");
            var pGyps = await _context.Products.FirstAsync(p => p.SKU == "FLW-GYP-WHT");
            var pEuc = await _context.Products.FirstAsync(p => p.SKU == "FLW-EUC-CIN");
            var pRus = await _context.Products.FirstAsync(p => p.SKU == "FLW-RUS-ITA");
            var pVaseCyl = await _context.Products.FirstAsync(p => p.SKU == "VAS-GLS-CYL");
            var pKraft = await _context.Products.FirstAsync(p => p.SKU == "WRP-KRF-PPR");
            var pMatWrap = await _context.Products.FirstAsync(p => p.SKU == "WRP-MAT-KOR");
            var pRibbon = await _context.Products.FirstAsync(p => p.SKU == "WRP-RIB-SAT");
            var pHatBox = await _context.Products.FirstAsync(p => p.SKU == "WRP-BOX-CYL");
            var pFoam = await _context.Products.FirstAsync(p => p.SKU == "ACC-OAS-BLK");

            var bqtRed = await _context.Products.FirstAsync(p => p.SKU == "BQT-ROSE-24");
            var bqtWht = await _context.Products.FirstAsync(p => p.SKU == "BQT-WHT-PUR");
            var bqtSun = await _context.Products.FirstAsync(p => p.SKU == "BQT-SUN-SUM");
            var bqtBox = await _context.Products.FirstAsync(p => p.SKU == "BQT-BOX-ROM");
            var bqtOrchid = await _context.Products.FirstAsync(p => p.SKU == "BQT-ORC-LUX");

            // Recipe 1: Royal Red 24
            if (!await _context.BouquetRecipes.AnyAsync(r => r.BouquetProductId == bqtRed.Id))
            {
                var r1 = new BouquetRecipe
                {
                    BouquetProductId = bqtRed.Id,
                    LaborCost = 15.00m,
                    PackagingCost = 5.00m,
                    Instructions = "Strip lower thorns, spiral stems at 45 degree angle. Crown with eucalyptus collar, wrap with Korean matte paper and tie double satin bow.",
                    IsActive = true
                };
                r1.Items.Add(new RecipeItem { ComponentProductId = pRedRose.Id, QuantityRequired = 24, WasteMarginPercentage = 5.0m });
                r1.Items.Add(new RecipeItem { ComponentProductId = pGyps.Id, QuantityRequired = 1, WasteMarginPercentage = 0m });
                r1.Items.Add(new RecipeItem { ComponentProductId = pEuc.Id, QuantityRequired = 1, WasteMarginPercentage = 0m });
                r1.Items.Add(new RecipeItem { ComponentProductId = pMatWrap.Id, QuantityRequired = 2, WasteMarginPercentage = 0m });
                r1.Items.Add(new RecipeItem { ComponentProductId = pRibbon.Id, QuantityRequired = 3.0m, WasteMarginPercentage = 0m });
                _context.BouquetRecipes.Add(r1);
            }

            // Recipe 2: White Symphony
            if (!await _context.BouquetRecipes.AnyAsync(r => r.BouquetProductId == bqtWht.Id))
            {
                var r2 = new BouquetRecipe
                {
                    BouquetProductId = bqtWht.Id,
                    LaborCost = 12.00m,
                    PackagingCost = 4.00m,
                    Instructions = "Carefully remove lily anthers to prevent pollen staining. Interlace with white avalanche roses and Italian ruscus.",
                    IsActive = true
                };
                r2.Items.Add(new RecipeItem { ComponentProductId = pWhtRose.Id, QuantityRequired = 12, WasteMarginPercentage = 5.0m });
                r2.Items.Add(new RecipeItem { ComponentProductId = pLily.Id, QuantityRequired = 3, WasteMarginPercentage = 0m });
                r2.Items.Add(new RecipeItem { ComponentProductId = pRus.Id, QuantityRequired = 1, WasteMarginPercentage = 0m });
                r2.Items.Add(new RecipeItem { ComponentProductId = pKraft.Id, QuantityRequired = 2, WasteMarginPercentage = 0m });
                r2.Items.Add(new RecipeItem { ComponentProductId = pRibbon.Id, QuantityRequired = 2.0m, WasteMarginPercentage = 0m });
                _context.BouquetRecipes.Add(r2);
            }

            // Recipe 3: Golden Sunshine
            if (!await _context.BouquetRecipes.AnyAsync(r => r.BouquetProductId == bqtSun.Id))
            {
                var r3 = new BouquetRecipe
                {
                    BouquetProductId = bqtSun.Id,
                    LaborCost = 10.00m,
                    PackagingCost = 3.00m,
                    Instructions = "Alternate sunflowers with yellow chrysanthemums for dome symmetry. Wrap in rustic natural kraft paper.",
                    IsActive = true
                };
                r3.Items.Add(new RecipeItem { ComponentProductId = pSunflower.Id, QuantityRequired = 5, WasteMarginPercentage = 0m });
                r3.Items.Add(new RecipeItem { ComponentProductId = pChrysan.Id, QuantityRequired = 8, WasteMarginPercentage = 0m });
                r3.Items.Add(new RecipeItem { ComponentProductId = pGyps.Id, QuantityRequired = 1, WasteMarginPercentage = 0m });
                r3.Items.Add(new RecipeItem { ComponentProductId = pKraft.Id, QuantityRequired = 2, WasteMarginPercentage = 0m });
                r3.Items.Add(new RecipeItem { ComponentProductId = pRibbon.Id, QuantityRequired = 2.0m, WasteMarginPercentage = 0m });
                _context.BouquetRecipes.Add(r3);
            }

            // Recipe 4: Velvet Romance Hat Box
            if (!await _context.BouquetRecipes.AnyAsync(r => r.BouquetProductId == bqtBox.Id))
            {
                var r4 = new BouquetRecipe
                {
                    BouquetProductId = bqtBox.Id,
                    LaborCost = 14.00m,
                    PackagingCost = 4.00m,
                    Instructions = "Hydrate foam brick with flower food, line velvet hat box with waterproof film. Insert cut pink roses in radial concentric tiers.",
                    IsActive = true
                };
                r4.Items.Add(new RecipeItem { ComponentProductId = pPnkRose.Id, QuantityRequired = 15, WasteMarginPercentage = 5.0m });
                r4.Items.Add(new RecipeItem { ComponentProductId = pGyps.Id, QuantityRequired = 1, WasteMarginPercentage = 0m });
                r4.Items.Add(new RecipeItem { ComponentProductId = pHatBox.Id, QuantityRequired = 1, WasteMarginPercentage = 0m });
                r4.Items.Add(new RecipeItem { ComponentProductId = pFoam.Id, QuantityRequired = 1, WasteMarginPercentage = 0m });
                _context.BouquetRecipes.Add(r4);
            }

            // Recipe 5: Orchid Grand Vase
            if (!await _context.BouquetRecipes.AnyAsync(r => r.BouquetProductId == bqtOrchid.Id))
            {
                var r5 = new BouquetRecipe
                {
                    BouquetProductId = bqtOrchid.Id,
                    LaborCost = 20.00m,
                    PackagingCost = 6.00m,
                    Instructions = "Fill cylinder vase with conditioned water. Base with hydrangeas and eucalyptus, arch violet orchids upward for dramatic height.",
                    IsActive = true
                };
                r5.Items.Add(new RecipeItem { ComponentProductId = pOrchid.Id, QuantityRequired = 6, WasteMarginPercentage = 0m });
                r5.Items.Add(new RecipeItem { ComponentProductId = pHydrangea.Id, QuantityRequired = 3, WasteMarginPercentage = 0m });
                r5.Items.Add(new RecipeItem { ComponentProductId = pEuc.Id, QuantityRequired = 1, WasteMarginPercentage = 0m });
                r5.Items.Add(new RecipeItem { ComponentProductId = pVaseCyl.Id, QuantityRequired = 1, WasteMarginPercentage = 0m });
                r5.Items.Add(new RecipeItem { ComponentProductId = pRibbon.Id, QuantityRequired = 2.0m, WasteMarginPercentage = 0m });
                _context.BouquetRecipes.Add(r5);
            }

            await _context.SaveChangesAsync();
        }

        private async Task SeedStockItemsAsync()
        {
            if (await _context.StockItems.CountAsync() >= 20)
            {
                return;
            }

            var wMain = await _context.Warehouses.FirstAsync(w => w.Code == "MAIN");
            var wCold = await _context.Warehouses.FirstAsync(w => w.Code == "COLD-ROOM");
            var wPkg = await _context.Warehouses.FirstOrDefaultAsync(w => w.Code == "PKG-ROOM") ?? wMain;
            var allProducts = await _context.Products.ToListAsync();

            foreach (var product in allProducts)
            {
                // Service products have no physical inventory
                if (product.ProductType == ProductType.Service)
                    continue;

                // Test Scenarios for specific products:
                // 1. Out of stock item: Brushed Gold Urn
                if (product.SKU == "VAS-MET-GLD")
                {
                    await AddOrUpdateStockItem(product.Id, wMain.Id, 0);
                    if (wPkg.Id != wMain.Id)
                    {
                        await AddOrUpdateStockItem(product.Id, wPkg.Id, 0);
                    }
                    continue;
                }

                // 2. Low stock item: Pink Peony (MinStock: 10, OnHand: 5)
                if (product.SKU == "FLW-PEO-PNK")
                {
                    await AddOrUpdateStockItem(product.Id, wMain.Id, 3);
                    await AddOrUpdateStockItem(product.Id, wCold.Id, 2);
                    continue;
                }

                // 3. Low stock item: Emerald Crystal Vase (MinStock: 8, OnHand: 4)
                if (product.SKU == "VAS-GLS-FLT")
                {
                    await AddOrUpdateStockItem(product.Id, wMain.Id, 2);
                    if (wPkg.Id != wMain.Id)
                    {
                        await AddOrUpdateStockItem(product.Id, wPkg.Id, 2);
                    }
                    continue;
                }

                // 4. Ribbon with decimal stock (e.g. 142.5 meters in Main, 250 in Pkg)
                if (product.SKU == "WRP-RIB-SAT")
                {
                    await AddOrUpdateStockItem(product.Id, wMain.Id, 142.5m);
                    if (wPkg.Id != wMain.Id)
                    {
                        await AddOrUpdateStockItem(product.Id, wPkg.Id, 250.0m);
                    }
                    continue;
                }

                // Fresh Flowers distribute between Main Floor and Cold Room
                if (product.ProductType == ProductType.FreshFlower)
                {
                    decimal mainQty = product.MinimumStockLevel * 1.5m;
                    decimal coldQty = product.MinimumStockLevel * 3.0m;
                    await AddOrUpdateStockItem(product.Id, wMain.Id, mainQty);
                    await AddOrUpdateStockItem(product.Id, wCold.Id, coldQty);
                }
                // Vases, Wraps, Accessories distribute between Main and Pkg Room
                else if (product.ProductType == ProductType.Vase ||
                         product.ProductType == ProductType.WrappingMaterial ||
                         product.ProductType == ProductType.Accessory ||
                         product.ProductType == ProductType.ArtificialFlower)
                {
                    decimal mainQty = Math.Max(10, product.MinimumStockLevel * 1.2m);
                    decimal pkgQty = Math.Max(20, product.MinimumStockLevel * 2.5m);
                    await AddOrUpdateStockItem(product.Id, wMain.Id, mainQty);
                    if (wPkg.Id != wMain.Id)
                    {
                        await AddOrUpdateStockItem(product.Id, wPkg.Id, pkgQty);
                    }
                }
                // Pre-assembled bouquets display in Main Store
                else if (product.ProductType == ProductType.Bouquet)
                {
                    await AddOrUpdateStockItem(product.Id, wMain.Id, 6);
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task AddOrUpdateStockItem(int productId, int warehouseId, decimal qtyOnHand)
        {
            var item = _context.StockItems.Local.FirstOrDefault(s => s.ProductId == productId && s.WarehouseId == warehouseId)
                       ?? await _context.StockItems.FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);
            if (item == null)
            {
                _context.StockItems.Add(new StockItem
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    QuantityOnHand = qtyOnHand,
                    QuantityReserved = 0
                });
            }
            else
            {
                item.QuantityOnHand = qtyOnHand;
            }
        }

        private async Task SeedPurchasingAsync()
        {
            if (await _context.PurchaseInvoices.CountAsync() >= 6)
            {
                return;
            }

            var sHolland = await _context.Suppliers.FirstAsync(s => s.Name.Contains("Holland"));
            var sPackaging = await _context.Suppliers.FirstAsync(s => s.Name.Contains("Bloom Packaging"));
            var sRiyadh = await _context.Suppliers.FirstAsync(s => s.Name.Contains("Riyadh"));
            var sMedellin = await _context.Suppliers.FirstAsync(s => s.Name.Contains("Medellin"));
            var sTuscany = await _context.Suppliers.FirstAsync(s => s.Name.Contains("Tuscany"));

            var wCold = await _context.Warehouses.FirstAsync(w => w.Code == "COLD-ROOM");
            var wMain = await _context.Warehouses.FirstAsync(w => w.Code == "MAIN");
            var wPkg = wMain;

            var pRedRose = await _context.Products.FirstAsync(p => p.SKU == "FLW-ROS-RED");
            var pWhtRose = await _context.Products.FirstAsync(p => p.SKU == "FLW-ROS-WHT");
            var pLily = await _context.Products.FirstAsync(p => p.SKU == "FLW-LIL-ORNT");
            var pHydrangea = await _context.Products.FirstAsync(p => p.SKU == "FLW-HYD-BLU");
            var pVaseCyl = await _context.Products.FirstAsync(p => p.SKU == "VAS-GLS-CYL");
            var pVaseCer = await _context.Products.FirstAsync(p => p.SKU == "VAS-CER-RND");
            var pRibbon = await _context.Products.FirstAsync(p => p.SKU == "WRP-RIB-SAT");
            var pKraft = await _context.Products.FirstAsync(p => p.SKU == "WRP-KRF-PPR");

            var now = DateTime.UtcNow;

            // Scenario 1: Posted & Fully Paid Dutch Flower Import (30 days ago)
            var pinv1 = new PurchaseInvoice
            {
                InvoiceNumber = "PINV-202609-0001",
                SupplierInvoiceNumber = "EXP-NL-98441",
                InvoiceDateUtc = now.AddDays(-30),
                DueDateUtc = now.AddDays(-15),
                SupplierId = sHolland.Id,
                WarehouseId = wCold.Id,
                Status = PurchaseInvoiceStatus.Posted,
                SubTotal = 1550.00m,
                DiscountAmount = 50.00m,
                TaxRatePercentage = 15.0m,
                TaxAmount = 225.00m,
                GrandTotal = 1725.00m,
                PaidAmount = 1725.00m,
                PostedAtUtc = now.AddDays(-30),
                Notes = "Air cargo shipment arrived intact at temperature 3.5°C"
            };
            pinv1.Items.Add(new PurchaseInvoiceItem { ProductId = pRedRose.Id, Quantity = 500, UnitCost = 1.70m, LineTotal = 850.00m });
            pinv1.Items.Add(new PurchaseInvoiceItem { ProductId = pLily.Id, Quantity = 200, UnitCost = 3.50m, LineTotal = 700.00m });
            pinv1.Payments.Add(new SupplierPayment
            {
                SupplierId = sHolland.Id,
                PaymentDateUtc = now.AddDays(-28),
                Amount = 1725.00m,
                PaymentMethod = PaymentMethod.BankTransfer,
                ReferenceNumber = "SWIFT-20260920-HOL",
                Notes = "Full wire payment"
            });
            _context.PurchaseInvoices.Add(pinv1);

            // Scenario 2: Posted & Partially Paid Packaging Order (20 days ago, outstanding balance)
            var pinv2 = new PurchaseInvoice
            {
                InvoiceNumber = "PINV-202609-0002",
                SupplierInvoiceNumber = "BP-90214",
                InvoiceDateUtc = now.AddDays(-20),
                DueDateUtc = now.AddDays(10),
                SupplierId = sPackaging.Id,
                WarehouseId = wPkg.Id,
                Status = PurchaseInvoiceStatus.Posted,
                SubTotal = 820.50m,
                DiscountAmount = 0.00m,
                TaxRatePercentage = 0.0m,
                TaxAmount = 0.00m,
                GrandTotal = 820.50m,
                PaidAmount = 500.00m,
                PostedAtUtc = now.AddDays(-20),
                Notes = "Packaging order on net-30 terms"
            };
            pinv2.Items.Add(new PurchaseInvoiceItem { ProductId = pRibbon.Id, Quantity = 500, UnitCost = 0.40m, LineTotal = 200.00m });
            pinv2.Items.Add(new PurchaseInvoiceItem { ProductId = pKraft.Id, Quantity = 300, UnitCost = 0.60m, LineTotal = 180.00m });
            pinv2.Items.Add(new PurchaseInvoiceItem { ProductId = pVaseCyl.Id, Quantity = 88, UnitCost = 5.00m, LineTotal = 440.50m });
            pinv2.Payments.Add(new SupplierPayment
            {
                SupplierId = sPackaging.Id,
                PaymentDateUtc = now.AddDays(-18),
                Amount = 500.00m,
                PaymentMethod = PaymentMethod.BankTransfer,
                ReferenceNumber = "TRF-PACK-500",
                Notes = "Partial payment made, balance due in 10 days"
            });
            _context.PurchaseInvoices.Add(pinv2);

            // Scenario 3: Partially Returned Purchase Invoice (12 days ago)
            var pinv3 = new PurchaseInvoice
            {
                InvoiceNumber = "PINV-202610-0003",
                SupplierInvoiceNumber = "RIY-INV-4412",
                InvoiceDateUtc = now.AddDays(-12),
                DueDateUtc = now.AddDays(5),
                SupplierId = sRiyadh.Id,
                WarehouseId = wCold.Id,
                Status = PurchaseInvoiceStatus.PartiallyReturned,
                SubTotal = 2400.00m,
                DiscountAmount = 100.00m,
                TaxRatePercentage = 0.0m,
                TaxAmount = 0.00m,
                GrandTotal = 2300.00m,
                PaidAmount = 200.00m,
                PostedAtUtc = now.AddDays(-12),
                Notes = "Local farm delivery with partial wilting on batch #2"
            };
            var itemHydrangea = new PurchaseInvoiceItem { ProductId = pHydrangea.Id, Quantity = 200, UnitCost = 5.00m, LineTotal = 1000.00m, ReturnedQuantity = 30 };
            var itemWhtRose = new PurchaseInvoiceItem { ProductId = pWhtRose.Id, Quantity = 700, UnitCost = 2.00m, LineTotal = 1400.00m, ReturnedQuantity = 0 };
            pinv3.Items.Add(itemHydrangea);
            pinv3.Items.Add(itemWhtRose);

            // Purchase Return record for 30 wilted hydrangeas
            var pret1 = new PurchaseReturn
            {
                PurchaseInvoice = pinv3,
                SupplierId = sRiyadh.Id,
                WarehouseId = wCold.Id,
                ReturnNumber = "PRET-202610-0001",
                ReturnDateUtc = now.AddDays(-10),
                Status = PurchaseReturnStatus.Completed,
                Reason = "Temperature fluctuation during farm transit caused petal dehydration",
                TotalRefundAmount = 150.00m,
                CompletedAtUtc = now.AddDays(-10),
                CompletedByUserId = "manager@flowershop.local"
            };
            pret1.Items.Add(new PurchaseReturnItem
            {
                PurchaseInvoiceItem = itemHydrangea,
                ProductId = pHydrangea.Id,
                Quantity = 30,
                UnitCost = 5.00m,
                LineRefundTotal = 150.00m,
                Disposition = ReturnDisposition.Discarded
            });
            pinv3.Returns.Add(pret1);
            _context.PurchaseInvoices.Add(pinv3);

            // Scenario 4: Fully Returned Purchase Invoice (5 days ago, Italian vases broken in transit)
            var pinv4 = new PurchaseInvoice
            {
                InvoiceNumber = "PINV-202610-0004",
                SupplierInvoiceNumber = "TUSC-8819",
                InvoiceDateUtc = now.AddDays(-7),
                SupplierId = sTuscany.Id,
                WarehouseId = wPkg.Id,
                Status = PurchaseInvoiceStatus.FullyReturned,
                SubTotal = 900.00m,
                DiscountAmount = 0m,
                TaxAmount = 0m,
                GrandTotal = 900.00m,
                PaidAmount = 0.00m,
                PostedAtUtc = now.AddDays(-7),
                Notes = "Ceramic pots pallet tipped over in freight transit"
            };
            var brokenCerItem = new PurchaseInvoiceItem { ProductId = pVaseCer.Id, Quantity = 100, UnitCost = 9.00m, LineTotal = 900.00m, ReturnedQuantity = 100 };
            pinv4.Items.Add(brokenCerItem);

            var pret2 = new PurchaseReturn
            {
                PurchaseInvoice = pinv4,
                SupplierId = sTuscany.Id,
                WarehouseId = wPkg.Id,
                ReturnNumber = "PRET-202610-0002",
                ReturnDateUtc = now.AddDays(-5),
                Status = PurchaseReturnStatus.Completed,
                Reason = "Entire shipment shattered during freight delivery; carrier claim filed",
                TotalRefundAmount = 900.00m,
                CompletedAtUtc = now.AddDays(-5),
                CompletedByUserId = "manager@flowershop.local"
            };
            pret2.Items.Add(new PurchaseReturnItem
            {
                PurchaseInvoiceItem = brokenCerItem,
                ProductId = pVaseCer.Id,
                Quantity = 100,
                UnitCost = 9.00m,
                LineRefundTotal = 900.00m,
                Disposition = ReturnDisposition.Damaged
            });
            pinv4.Returns.Add(pret2);
            _context.PurchaseInvoices.Add(pinv4);

            // Scenario 5: Draft Purchase Invoice (Waiting for manager approval/reception)
            var pinvDraft = new PurchaseInvoice
            {
                InvoiceNumber = "PINV-202610-0005",
                SupplierInvoiceNumber = "EST-MED-2026-9",
                InvoiceDateUtc = now.AddDays(-1),
                SupplierId = sMedellin.Id,
                WarehouseId = wCold.Id,
                Status = PurchaseInvoiceStatus.Draft,
                SubTotal = 3200.00m,
                DiscountAmount = 200.00m,
                TaxAmount = 0.00m,
                GrandTotal = 3000.00m,
                PaidAmount = 0.00m,
                Notes = "Pre-order quotation for upcoming Mother's Day and wedding season rush"
            };
            pinvDraft.Items.Add(new PurchaseInvoiceItem { ProductId = pRedRose.Id, Quantity = 1000, UnitCost = 1.60m, LineTotal = 1600.00m });
            pinvDraft.Items.Add(new PurchaseInvoiceItem { ProductId = pWhtRose.Id, Quantity = 1000, UnitCost = 1.60m, LineTotal = 1600.00m });
            _context.PurchaseInvoices.Add(pinvDraft);

            // Scenario 6: Cancelled Purchase Invoice
            var pinvCancelled = new PurchaseInvoice
            {
                InvoiceNumber = "PINV-202610-0006",
                SupplierInvoiceNumber = "FLH-CAN-001",
                InvoiceDateUtc = now.AddDays(-2),
                SupplierId = sHolland.Id,
                WarehouseId = wCold.Id,
                Status = PurchaseInvoiceStatus.Cancelled,
                SubTotal = 600.00m,
                GrandTotal = 600.00m,
                PaidAmount = 0.00m,
                Notes = "Cancelled due to supplier stock shortage at Amsterdam airport"
            };
            pinvCancelled.Items.Add(new PurchaseInvoiceItem { ProductId = pLily.Id, Quantity = 171, UnitCost = 3.50m, LineTotal = 600.00m });
            _context.PurchaseInvoices.Add(pinvCancelled);

            await _context.SaveChangesAsync();
        }

        private async Task SeedSalesAsync()
        {
            if (await _context.Invoices.CountAsync() >= 15)
            {
                return;
            }

            var wMain = await _context.Warehouses.FirstAsync(w => w.Code == "MAIN");
            var custWalkIn = await _context.Customers.FirstAsync(c => c.Name.Contains("Walk-in"));
            var custRitz = await _context.Customers.FirstAsync(c => c.Name.Contains("Ritz Royal"));
            var custHyatt = await _context.Customers.FirstAsync(c => c.Name.Contains("Grand Hyatt"));
            var custSophia = await _context.Customers.FirstAsync(c => c.Name.Contains("Sophia"));
            var custMiller = await _context.Customers.FirstAsync(c => c.Name.Contains("David Miller"));
            var custCafe = await _context.Customers.FirstAsync(c => c.Name.Contains("Bloom Café"));

            var pRedRose = await _context.Products.FirstAsync(p => p.SKU == "FLW-ROS-RED");
            var pWhtRose = await _context.Products.FirstAsync(p => p.SKU == "FLW-ROS-WHT");
            var pPnkRose = await _context.Products.FirstAsync(p => p.SKU == "FLW-ROS-PNK");
            var pLily = await _context.Products.FirstAsync(p => p.SKU == "FLW-LIL-ORNT");
            var pSunflower = await _context.Products.FirstAsync(p => p.SKU == "FLW-SNF-GLD");
            var pTulip = await _context.Products.FirstAsync(p => p.SKU == "FLW-TUL-YEL");
            var pVaseCyl = await _context.Products.FirstAsync(p => p.SKU == "VAS-GLS-CYL");
            var pVaseCer = await _context.Products.FirstAsync(p => p.SKU == "VAS-CER-RND");
            var pCard = await _context.Products.FirstAsync(p => p.SKU == "ACC-CRD-LUX");
            var pTeddy = await _context.Products.FirstAsync(p => p.SKU == "ACC-TOY-BR");
            var pDelivery = await _context.Products.FirstAsync(p => p.SKU == "SRV-DEL-EXP");
            var pSetup = await _context.Products.FirstAsync(p => p.SKU == "SRV-EVT-STP");

            var bqtRed = await _context.Products.FirstAsync(p => p.SKU == "BQT-ROSE-24");
            var bqtWht = await _context.Products.FirstAsync(p => p.SKU == "BQT-WHT-PUR");
            var bqtSun = await _context.Products.FirstAsync(p => p.SKU == "BQT-SUN-SUM");
            var bqtBox = await _context.Products.FirstAsync(p => p.SKU == "BQT-BOX-ROM");

            var pGyps = await _context.Products.FirstAsync(p => p.SKU == "FLW-GYP-WHT");
            var pEuc = await _context.Products.FirstAsync(p => p.SKU == "FLW-EUC-CIN");
            var pMatWrap = await _context.Products.FirstAsync(p => p.SKU == "WRP-MAT-KOR");
            var pRibbon = await _context.Products.FirstAsync(p => p.SKU == "WRP-RIB-SAT");
            var pHatBox = await _context.Products.FirstAsync(p => p.SKU == "WRP-BOX-CYL");
            var pFoam = await _context.Products.FirstAsync(p => p.SKU == "ACC-OAS-BLK");

            var now = DateTime.UtcNow;

            // 1. POS Cash Walk-in Sale with exact Change Given (Today)
            var inv1 = new Invoice
            {
                InvoiceNumber = "INV-202610-0001",
                InvoiceDateUtc = now.AddHours(-2),
                CustomerId = custWalkIn.Id,
                Status = InvoiceStatus.FullyPaid,
                SubTotal = 58.00m,
                DiscountAmount = 0.00m,
                TaxRatePercentage = 0.0m,
                TaxAmount = 0.00m,
                GrandTotal = 58.00m,
                PaidAmount = 58.00m,
                AmountReceived = 100.00m,
                ChangeGiven = 42.00m,
                Notes = "POS Counter: Customer paid with $100 bill"
            };
            inv1.Items.Add(new InvoiceItem { ProductId = pRedRose.Id, WarehouseId = wMain.Id, Quantity = 10, UnitPrice = 4.50m, UnitCost = 1.80m, LineTotal = 45.00m });
            inv1.Items.Add(new InvoiceItem { ProductId = pCard.Id, WarehouseId = wMain.Id, Quantity = 1, UnitPrice = 3.50m, UnitCost = 0.80m, LineTotal = 3.50m });
            inv1.Items.Add(new InvoiceItem { ProductId = pSunflower.Id, WarehouseId = wMain.Id, Quantity = 1, UnitPrice = 5.00m, UnitCost = 2.00m, LineTotal = 5.00m });
            inv1.Payments.Add(new Payment { PaymentDateUtc = now.AddHours(-2), Amount = 58.00m, PaymentMethod = PaymentMethod.Cash, Notes = "Cash at till" });
            _context.Invoices.Add(inv1);

            // 2. Bouquet Sale with Component Consumption Recorded (Yesterday)
            var inv2 = new Invoice
            {
                InvoiceNumber = "INV-202610-0002",
                InvoiceDateUtc = now.AddDays(-1),
                CustomerId = custSophia.Id,
                Status = InvoiceStatus.FullyPaid,
                SubTotal = 163.50m,
                DiscountAmount = 10.00m,
                TaxRatePercentage = 5.0m,
                TaxAmount = 7.68m,
                GrandTotal = 161.18m,
                PaidAmount = 161.18m,
                AmountReceived = 161.18m,
                ChangeGiven = 0.00m,
                Notes = "VIP client birthday order delivered via Express courier"
            };
            var bqtItem = new InvoiceItem
            {
                ProductId = bqtRed.Id,
                WarehouseId = wMain.Id,
                Quantity = 1,
                UnitPrice = 145.00m,
                UnitCost = 62.50m,
                LineTotal = 145.00m,
                IsBouquetRecipeDeduction = true
            };
            // Detailed component consumption records
            bqtItem.ComponentConsumptions.Add(new InvoiceItemComponentConsumption { ComponentProductId = pRedRose.Id, QuantityConsumed = 25.2m, UnitCost = 1.80m });
            bqtItem.ComponentConsumptions.Add(new InvoiceItemComponentConsumption { ComponentProductId = pGyps.Id, QuantityConsumed = 1.0m, UnitCost = 5.50m });
            bqtItem.ComponentConsumptions.Add(new InvoiceItemComponentConsumption { ComponentProductId = pEuc.Id, QuantityConsumed = 1.0m, UnitCost = 3.80m });
            bqtItem.ComponentConsumptions.Add(new InvoiceItemComponentConsumption { ComponentProductId = pMatWrap.Id, QuantityConsumed = 2.0m, UnitCost = 0.90m });
            bqtItem.ComponentConsumptions.Add(new InvoiceItemComponentConsumption { ComponentProductId = pRibbon.Id, QuantityConsumed = 3.0m, UnitCost = 0.40m });
            inv2.Items.Add(bqtItem);
            inv2.Items.Add(new InvoiceItem { ProductId = pTeddy.Id, WarehouseId = wMain.Id, Quantity = 1, UnitPrice = 18.00m, UnitCost = 6.50m, LineTotal = 18.00m });
            inv2.Payments.Add(new Payment { PaymentDateUtc = now.AddDays(-1), Amount = 161.18m, PaymentMethod = PaymentMethod.CreditCard, ReferenceNumber = "AUTH-489201", Notes = "Visa contact-less" });
            _context.Invoices.Add(inv2);

            // 3. Corporate Wedding Event: Partially Paid Invoice (Accounts Receivable test)
            var inv3 = new Invoice
            {
                InvoiceNumber = "INV-202610-0003",
                InvoiceDateUtc = now.AddDays(-8),
                DueDateUtc = now.AddDays(7),
                CustomerId = custRitz.Id,
                Status = InvoiceStatus.PartiallyPaid,
                SubTotal = 3450.00m,
                DiscountAmount = 250.00m,
                TaxRatePercentage = 15.0m,
                TaxAmount = 480.00m,
                GrandTotal = 3680.00m,
                PaidAmount = 2000.00m,
                Notes = "Grand Ballroom decor package, 50% deposit received upon contract signing"
            };
            inv3.Items.Add(new InvoiceItem { ProductId = bqtWht.Id, WarehouseId = wMain.Id, Quantity = 10, UnitPrice = 110.00m, UnitCost = 46.20m, LineTotal = 1100.00m });
            inv3.Items.Add(new InvoiceItem { ProductId = pLily.Id, WarehouseId = wMain.Id, Quantity = 80, UnitPrice = 8.00m, UnitCost = 3.50m, LineTotal = 640.00m });
            inv3.Items.Add(new InvoiceItem { ProductId = pVaseCer.Id, WarehouseId = wMain.Id, Quantity = 20, UnitPrice = 22.00m, UnitCost = 9.00m, LineTotal = 440.00m });
            inv3.Items.Add(new InvoiceItem { ProductId = pSetup.Id, WarehouseId = wMain.Id, Quantity = 5, UnitPrice = 120.00m, UnitCost = 40.00m, LineTotal = 600.00m });
            inv3.Items.Add(new InvoiceItem { ProductId = pDelivery.Id, WarehouseId = wMain.Id, Quantity = 4, UnitPrice = 15.00m, UnitCost = 5.00m, LineTotal = 60.00m });
            inv3.Payments.Add(new Payment { PaymentDateUtc = now.AddDays(-8), Amount = 2000.00m, PaymentMethod = PaymentMethod.BankTransfer, ReferenceNumber = "WIRE-RITZ-DEP", Notes = "Deposit wire received" });
            _context.Invoices.Add(inv3);

            // 4. Partially Returned Sales Invoice (Customer returned 2 vases)
            var inv4 = new Invoice
            {
                InvoiceNumber = "INV-202610-0004",
                InvoiceDateUtc = now.AddDays(-14),
                CustomerId = custMiller.Id,
                Status = InvoiceStatus.PartiallyReturned,
                SubTotal = 252.00m,
                DiscountAmount = 12.00m,
                TaxRatePercentage = 0.0m,
                TaxAmount = 0.00m,
                GrandTotal = 240.00m,
                PaidAmount = 240.00m,
                Notes = "Table centerpieces for bridal brunch"
            };
            var itemVases = new InvoiceItem { ProductId = pVaseCyl.Id, WarehouseId = wMain.Id, Quantity = 6, UnitPrice = 14.00m, UnitCost = 5.00m, LineTotal = 84.00m, ReturnedQuantity = 2 };
            inv4.Items.Add(itemVases);
            inv4.Items.Add(new InvoiceItem { ProductId = pTulip.Id, WarehouseId = wMain.Id, Quantity = 6, UnitPrice = 18.00m, UnitCost = 8.50m, LineTotal = 108.00m });
            inv4.Items.Add(new InvoiceItem { ProductId = bqtSun.Id, WarehouseId = wMain.Id, Quantity = 1, UnitPrice = 75.00m, UnitCost = 31.80m, LineTotal = 60.00m });
            inv4.Payments.Add(new Payment { PaymentDateUtc = now.AddDays(-14), Amount = 240.00m, PaymentMethod = PaymentMethod.CreditCard, ReferenceNumber = "MC-88120" });

            // Sales Return for 2 unused cylinder vases (Restocked into inventory)
            var ret1 = new InvoiceReturn
            {
                Invoice = inv4,
                ReturnNumber = "SRET-202610-0001",
                ReturnDateUtc = now.AddDays(-12),
                Reason = "Customer ordered extra table centerpieces; 2 vases remained unused",
                TotalRefundAmount = 28.00m,
                Status = ReturnStatus.Completed,
                RefundMethod = "CreditCard",
                CreatedByUserId = "cashier@flowershop.local"
            };
            ret1.Items.Add(new ReturnItem
            {
                InvoiceItem = itemVases,
                Quantity = 2,
                RefundUnitPrice = 14.00m,
                LineRefundTotal = 28.00m,
                RestockToWarehouseId = wMain.Id,
                Disposition = ReturnDisposition.Restocked
            });
            inv4.Returns.Add(ret1);
            _context.Invoices.Add(inv4);

            // 5. Fully Returned Sales Invoice (Damaged during transit, refunded to cash)
            var inv5 = new Invoice
            {
                InvoiceNumber = "INV-202610-0005",
                InvoiceDateUtc = now.AddDays(-20),
                CustomerId = custWalkIn.Id,
                Status = InvoiceStatus.FullyReturned,
                SubTotal = 135.00m,
                GrandTotal = 135.00m,
                PaidAmount = 135.00m,
                Notes = "Hat box arrangement dropped by courier"
            };
            var retItemHatBox = new InvoiceItem { ProductId = bqtBox.Id, WarehouseId = wMain.Id, Quantity = 1, UnitPrice = 135.00m, UnitCost = 55.40m, LineTotal = 135.00m, ReturnedQuantity = 1 };
            inv5.Items.Add(retItemHatBox);
            inv5.Payments.Add(new Payment { PaymentDateUtc = now.AddDays(-20), Amount = 135.00m, PaymentMethod = PaymentMethod.DebitCard, ReferenceNumber = "DEBIT-901" });

            var ret2 = new InvoiceReturn
            {
                Invoice = inv5,
                ReturnNumber = "SRET-202610-0002",
                ReturnDateUtc = now.AddDays(-19),
                Reason = "Velvet box crushed in vehicle accident, stems snapped",
                TotalRefundAmount = 135.00m,
                Status = ReturnStatus.Completed,
                RefundMethod = "Cash",
                CreatedByUserId = "manager@flowershop.local"
            };
            ret2.Items.Add(new ReturnItem
            {
                InvoiceItem = retItemHatBox,
                Quantity = 1,
                RefundUnitPrice = 135.00m,
                LineRefundTotal = 135.00m,
                RestockToWarehouseId = null,
                Disposition = ReturnDisposition.Damaged // Damaged disposition, not restocked!
            });
            inv5.Returns.Add(ret2);
            _context.Invoices.Add(inv5);

            // 6. Weekly Recurring Café Subscription (15 days ago, Fully Paid Debit Card)
            var inv6 = new Invoice
            {
                InvoiceNumber = "INV-202610-0006",
                InvoiceDateUtc = now.AddDays(-15),
                CustomerId = custCafe.Id,
                Status = InvoiceStatus.FullyPaid,
                SubTotal = 150.00m,
                DiscountAmount = 15.00m,
                TaxAmount = 0.00m,
                GrandTotal = 135.00m,
                PaidAmount = 135.00m,
                Notes = "Weekly café fresh floral table bar arrangements"
            };
            inv6.Items.Add(new InvoiceItem { ProductId = pSunflower.Id, WarehouseId = wMain.Id, Quantity = 15, UnitPrice = 5.00m, UnitCost = 2.00m, LineTotal = 75.00m });
            inv6.Items.Add(new InvoiceItem { ProductId = pTulip.Id, WarehouseId = wMain.Id, Quantity = 4, UnitPrice = 18.00m, UnitCost = 8.50m, LineTotal = 72.00m });
            inv6.Payments.Add(new Payment { PaymentDateUtc = now.AddDays(-15), Amount = 135.00m, PaymentMethod = PaymentMethod.DebitCard, ReferenceNumber = "REC-CAFE-WK42" });
            _context.Invoices.Add(inv6);

            // 7. Confirmed Invoice Awaiting Payment (Grand Hyatt VIP Suite)
            var inv7 = new Invoice
            {
                InvoiceNumber = "INV-202610-0007",
                InvoiceDateUtc = now.AddDays(-3),
                DueDateUtc = now.AddDays(12),
                CustomerId = custHyatt.Id,
                Status = InvoiceStatus.Confirmed,
                SubTotal = 1250.00m,
                DiscountAmount = 50.00m,
                TaxRatePercentage = 15.0m,
                TaxAmount = 180.00m,
                GrandTotal = 1380.00m,
                PaidAmount = 0.00m,
                Notes = "VIP Diplomatic Suite floral dressing on monthly corporate billing account"
            };
            inv7.Items.Add(new InvoiceItem { ProductId = bqtRed.Id, WarehouseId = wMain.Id, Quantity = 4, UnitPrice = 145.00m, UnitCost = 62.50m, LineTotal = 580.00m });
            inv7.Items.Add(new InvoiceItem { ProductId = bqtWht.Id, WarehouseId = wMain.Id, Quantity = 4, UnitPrice = 110.00m, UnitCost = 46.20m, LineTotal = 440.00m });
            inv7.Items.Add(new InvoiceItem { ProductId = pDelivery.Id, WarehouseId = wMain.Id, Quantity = 2, UnitPrice = 15.00m, UnitCost = 5.00m, LineTotal = 30.00m });
            inv7.Items.Add(new InvoiceItem { ProductId = pSetup.Id, WarehouseId = wMain.Id, Quantity = 1, UnitPrice = 120.00m, UnitCost = 40.00m, LineTotal = 120.00m });
            _context.Invoices.Add(inv7);

            // 8. Draft Sales Quote / Invoice
            var invDraft = new Invoice
            {
                InvoiceNumber = "INV-202610-0008",
                InvoiceDateUtc = now.AddHours(-5),
                CustomerId = custRitz.Id,
                Status = InvoiceStatus.Draft,
                SubTotal = 540.00m,
                GrandTotal = 540.00m,
                PaidAmount = 0.00m,
                Notes = "Draft quote for upcoming fashion week VIP lounge"
            };
            invDraft.Items.Add(new InvoiceItem { ProductId = bqtBox.Id, WarehouseId = wMain.Id, Quantity = 4, UnitPrice = 135.00m, UnitCost = 55.40m, LineTotal = 540.00m });
            _context.Invoices.Add(invDraft);

            // 9. Cancelled Sales Invoice
            var invCancelled = new Invoice
            {
                InvoiceNumber = "INV-202610-0009",
                InvoiceDateUtc = now.AddDays(-6),
                CustomerId = custWalkIn.Id,
                Status = InvoiceStatus.Cancelled,
                SubTotal = 95.00m,
                GrandTotal = 95.00m,
                PaidAmount = 0.00m,
                Notes = "Customer cancelled order before bouquet arrangement was assembled"
            };
            invCancelled.Items.Add(new InvoiceItem { ProductId = pRedRose.Id, WarehouseId = wMain.Id, Quantity = 20, UnitPrice = 4.50m, UnitCost = 1.80m, LineTotal = 90.00m });
            _context.Invoices.Add(invCancelled);

            // 10-15. Add historical daily invoices across previous 30 days for rich reporting trends
            for (int i = 1; i <= 8; i++)
            {
                var pastDate = now.AddDays(-i * 3);
                var histInv = new Invoice
                {
                    InvoiceNumber = $"INV-202609-{1000 + i}",
                    InvoiceDateUtc = pastDate,
                    CustomerId = custWalkIn.Id,
                    Status = InvoiceStatus.FullyPaid,
                    SubTotal = 90.00m + (i * 12.50m),
                    DiscountAmount = (i % 2 == 0) ? 5.00m : 0.00m,
                    TaxRatePercentage = 0m,
                    TaxAmount = 0m,
                    GrandTotal = 90.00m + (i * 12.50m) - ((i % 2 == 0) ? 5.00m : 0.00m),
                    PaidAmount = 90.00m + (i * 12.50m) - ((i % 2 == 0) ? 5.00m : 0.00m),
                    AmountReceived = 150.00m,
                    ChangeGiven = 150.00m - (90.00m + (i * 12.50m) - ((i % 2 == 0) ? 5.00m : 0.00m)),
                    Notes = $"Walk-in POS daily register transaction on {pastDate:yyyy-MM-dd}"
                };
                histInv.Items.Add(new InvoiceItem { ProductId = pRedRose.Id, WarehouseId = wMain.Id, Quantity = 8 + i, UnitPrice = 4.50m, UnitCost = 1.80m, LineTotal = (8 + i) * 4.50m });
                histInv.Items.Add(new InvoiceItem { ProductId = pWhtRose.Id, WarehouseId = wMain.Id, Quantity = 6 + i, UnitPrice = 4.50m, UnitCost = 1.80m, LineTotal = (6 + i) * 4.50m });
                histInv.Payments.Add(new Payment
                {
                    PaymentDateUtc = pastDate,
                    Amount = histInv.GrandTotal,
                    PaymentMethod = (i % 2 == 0) ? PaymentMethod.CreditCard : PaymentMethod.Cash,
                    ReferenceNumber = $"POS-TXN-{i}004"
                });
                _context.Invoices.Add(histInv);
            }

            await _context.SaveChangesAsync();
        }

        private async Task SeedStocktakingAsync()
        {
            if (await _context.Stocktakings.CountAsync() >= 3)
            {
                return;
            }

            var wMain = await _context.Warehouses.FirstAsync(w => w.Code == "MAIN");
            var wCold = await _context.Warehouses.FirstAsync(w => w.Code == "COLD-ROOM");

            var pRedRose = await _context.Products.FirstAsync(p => p.SKU == "FLW-ROS-RED");
            var pWhtRose = await _context.Products.FirstAsync(p => p.SKU == "FLW-ROS-WHT");
            var pVaseCyl = await _context.Products.FirstAsync(p => p.SKU == "VAS-GLS-CYL");
            var pLily = await _context.Products.FirstAsync(p => p.SKU == "FLW-LIL-ORNT");
            var pSunflower = await _context.Products.FirstAsync(p => p.SKU == "FLW-SNF-GLD");

            var now = DateTime.UtcNow;

            // Session 1: Posted Stocktaking with Shortages and Surpluses (Audit variance testing)
            var st1 = new Stocktaking
            {
                StocktakingNumber = "STK-202610-0001",
                WarehouseId = wMain.Id,
                StartDateUtc = now.AddDays(-7),
                CompletedDateUtc = now.AddDays(-7).AddHours(4),
                ApprovedDateUtc = now.AddDays(-6),
                CreatedByUserId = "inventory@flowershop.local",
                ApprovedByUserId = "manager@flowershop.local",
                Status = StocktakingStatus.Posted,
                Reason = "Monthly physical inventory verification for Main Floor",
                Notes = "Variance adjustment posted after manager physical recount",
                TotalShortageQuantity = 5.0m,
                TotalSurplusQuantity = 2.0m,
                NetDifferenceQuantity = -3.0m,
                TotalVarianceCost = -9.00m // Net financial impact
            };

            // Line 1: Shortage of 5 Red Roses (Stem breakage during customer handling)
            st1.Lines.Add(new StocktakingLine
            {
                ProductId = pRedRose.Id,
                SystemQuantity = 80.0m,
                CountedQuantity = 75.0m,
                DifferenceQuantity = -5.0m,
                CurrentSystemQuantity = 75.0m,
                UnitCost = 1.80m,
                VarianceCost = -9.00m,
                Reason = "Stem breakage and petal decay during retail store handling"
            });

            // Line 2: Surplus of 2 Cylinder Vases (Found unpacked in display storage)
            st1.Lines.Add(new StocktakingLine
            {
                ProductId = pVaseCyl.Id,
                SystemQuantity = 18.0m,
                CountedQuantity = 20.0m,
                DifferenceQuantity = 2.0m,
                CurrentSystemQuantity = 20.0m,
                UnitCost = 5.00m,
                VarianceCost = 10.00m,
                Reason = "Found additional unpacked pieces behind showroom cabinet"
            });

            // Line 3: Exact count (No variance)
            st1.Lines.Add(new StocktakingLine
            {
                ProductId = pSunflower.Id,
                SystemQuantity = 30.0m,
                CountedQuantity = 30.0m,
                DifferenceQuantity = 0.0m,
                CurrentSystemQuantity = 30.0m,
                UnitCost = 2.00m,
                VarianceCost = 0.00m,
                Reason = "Count matched system records perfectly"
            });

            _context.Stocktakings.Add(st1);

            // Session 2: Reviewed Stocktaking in Cold Storage (Awaiting Admin approval)
            var st2 = new Stocktaking
            {
                StocktakingNumber = "STK-202610-0002",
                WarehouseId = wCold.Id,
                StartDateUtc = now.AddDays(-2),
                CompletedDateUtc = now.AddDays(-1),
                CreatedByUserId = "inventory@flowershop.local",
                Status = StocktakingStatus.Reviewed,
                Reason = "Weekly cold room refrigerated stem audit",
                Notes = "Submitted for management final sign-off"
            };
            st2.Lines.Add(new StocktakingLine
            {
                ProductId = pLily.Id,
                SystemQuantity = 65.0m,
                CountedQuantity = 62.0m,
                DifferenceQuantity = -3.0m,
                CurrentSystemQuantity = 65.0m,
                UnitCost = 3.50m,
                VarianceCost = -10.50m,
                Reason = "Three heads damaged by condensation drip"
            });
            _context.Stocktakings.Add(st2);

            // Session 3: Draft Stocktaking
            var st3 = new Stocktaking
            {
                StocktakingNumber = "STK-202610-0003",
                WarehouseId = wMain.Id,
                StartDateUtc = now.AddHours(-1),
                CreatedByUserId = "inventory@flowershop.local",
                Status = StocktakingStatus.Draft,
                Reason = "Mid-week spot check"
            };
            _context.Stocktakings.Add(st3);

            await _context.SaveChangesAsync();
        }

        private async Task SeedStockMovementsAsync()
        {
            if (await _context.StockMovements.CountAsync() >= 15)
            {
                return;
            }

            var wMain = await _context.Warehouses.FirstAsync(w => w.Code == "MAIN");
            var wCold = await _context.Warehouses.FirstAsync(w => w.Code == "COLD-ROOM");
            var wPkg = wMain;

            var pRedRose = await _context.Products.FirstAsync(p => p.SKU == "FLW-ROS-RED");
            var pWhtRose = await _context.Products.FirstAsync(p => p.SKU == "FLW-ROS-WHT");
            var pLily = await _context.Products.FirstAsync(p => p.SKU == "FLW-LIL-ORNT");
            var pHydrangea = await _context.Products.FirstAsync(p => p.SKU == "FLW-HYD-BLU");
            var pVaseCyl = await _context.Products.FirstAsync(p => p.SKU == "VAS-GLS-CYL");
            var pRibbon = await _context.Products.FirstAsync(p => p.SKU == "WRP-RIB-SAT");

            var now = DateTime.UtcNow;

            var movements = new List<StockMovement>
            {
                // PurchaseIn movements
                new StockMovement
                {
                    ProductId = pRedRose.Id,
                    WarehouseId = wCold.Id,
                    MovementType = MovementType.PurchaseIn,
                    Quantity = 500,
                    UnitCost = 1.70m,
                    ReferenceType = "PurchaseInvoice",
                    ReferenceId = "PINV-202609-0001",
                    Notes = "Initial air shipment receipt from Flora Wholesale Holland",
                    CreatedAtUtc = now.AddDays(-30)
                },
                new StockMovement
                {
                    ProductId = pLily.Id,
                    WarehouseId = wCold.Id,
                    MovementType = MovementType.PurchaseIn,
                    Quantity = 200,
                    UnitCost = 3.50m,
                    ReferenceType = "PurchaseInvoice",
                    ReferenceId = "PINV-202609-0001",
                    Notes = "Initial oriental lily receipt",
                    CreatedAtUtc = now.AddDays(-30)
                },
                new StockMovement
                {
                    ProductId = pRibbon.Id,
                    WarehouseId = wPkg.Id,
                    MovementType = MovementType.PurchaseIn,
                    Quantity = 500,
                    UnitCost = 0.40m,
                    ReferenceType = "PurchaseInvoice",
                    ReferenceId = "PINV-202609-0002",
                    Notes = "Bulk satin ribbon roll inbound from Bloom Packaging",
                    CreatedAtUtc = now.AddDays(-20)
                },

                // Transfer between Cold Room and Main Store
                new StockMovement
                {
                    ProductId = pRedRose.Id,
                    WarehouseId = wCold.Id,
                    MovementType = MovementType.TransferOut,
                    Quantity = -100,
                    UnitCost = 1.70m,
                    ReferenceType = "Transfer",
                    ReferenceId = "TRF-202610-001",
                    Notes = "Stock transferred from cold storage to retail display",
                    CreatedAtUtc = now.AddDays(-15)
                },
                new StockMovement
                {
                    ProductId = pRedRose.Id,
                    WarehouseId = wMain.Id,
                    MovementType = MovementType.TransferIn,
                    Quantity = 100,
                    UnitCost = 1.70m,
                    ReferenceType = "Transfer",
                    ReferenceId = "TRF-202610-001",
                    Notes = "Stock received at retail display",
                    CreatedAtUtc = now.AddDays(-15)
                },

                // SalesOut movements
                new StockMovement
                {
                    ProductId = pRedRose.Id,
                    WarehouseId = wMain.Id,
                    MovementType = MovementType.SalesOut,
                    Quantity = -10,
                    UnitCost = 1.80m,
                    ReferenceType = "Invoice",
                    ReferenceId = "INV-202610-0001",
                    Notes = "POS counter checkout deduction",
                    CreatedAtUtc = now.AddHours(-2)
                },

                // Bouquet Assembly consumption
                new StockMovement
                {
                    ProductId = pRedRose.Id,
                    WarehouseId = wMain.Id,
                    MovementType = MovementType.BouquetAssemblyOut,
                    Quantity = -25.2m,
                    UnitCost = 1.80m,
                    ReferenceType = "Invoice",
                    ReferenceId = "INV-202610-0002",
                    Notes = "Component consumed for Royal Red 24 bouquet order",
                    CreatedAtUtc = now.AddDays(-1)
                },

                // ReturnIn (Restocked)
                new StockMovement
                {
                    ProductId = pVaseCyl.Id,
                    WarehouseId = wMain.Id,
                    MovementType = MovementType.ReturnIn,
                    Quantity = 2,
                    UnitCost = 5.00m,
                    ReferenceType = "Return",
                    ReferenceId = "SRET-202610-0001",
                    Notes = "Customer returned unused cylinder vases into stock",
                    CreatedAtUtc = now.AddDays(-12)
                },

                // PurchaseReturnOut
                new StockMovement
                {
                    ProductId = pHydrangea.Id,
                    WarehouseId = wCold.Id,
                    MovementType = MovementType.PurchaseReturnOut,
                    Quantity = -30,
                    UnitCost = 5.00m,
                    ReferenceType = "PurchaseReturn",
                    ReferenceId = "PRET-202610-0001",
                    Notes = "Wilted hydrangeas returned to supplier claim",
                    CreatedAtUtc = now.AddDays(-10)
                },

                // StocktakingAdjustment (Physical Audit reconciliation)
                new StockMovement
                {
                    ProductId = pRedRose.Id,
                    WarehouseId = wMain.Id,
                    MovementType = MovementType.StocktakingAdjustment,
                    Quantity = -5,
                    UnitCost = 1.80m,
                    ReferenceType = "Stocktaking",
                    ReferenceId = "STK-202610-0001",
                    Notes = "Physical inventory shortage write-off: breakage",
                    CreatedAtUtc = now.AddDays(-6)
                },
                new StockMovement
                {
                    ProductId = pVaseCyl.Id,
                    WarehouseId = wMain.Id,
                    MovementType = MovementType.StocktakingAdjustment,
                    Quantity = 2,
                    UnitCost = 5.00m,
                    ReferenceType = "Stocktaking",
                    ReferenceId = "STK-202610-0001",
                    Notes = "Physical inventory surplus reconciliation: found display stock",
                    CreatedAtUtc = now.AddDays(-6)
                },

                // DamagedDiscardOut (Spoilage write-off)
                new StockMovement
                {
                    ProductId = pWhtRose.Id,
                    WarehouseId = wCold.Id,
                    MovementType = MovementType.DamagedDiscardOut,
                    Quantity = -8,
                    UnitCost = 1.80m,
                    ReferenceType = "QualityAudit",
                    ReferenceId = "WASTE-202610-01",
                    Notes = "Botrytis petal mold detected and safely discarded",
                    CreatedAtUtc = now.AddDays(-4)
                }
            };

            _context.StockMovements.AddRange(movements);
            await _context.SaveChangesAsync();
        }
    }
}
