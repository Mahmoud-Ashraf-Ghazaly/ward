using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Entities.Inventory;
using ward.Entities.Production;
using ward.Models.Production;
using ward.Services.Production;
using Xunit;

namespace ward.Tests
{
    public class BouquetRecipeTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public BouquetRecipeTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new ApplicationDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private async Task<(int BouquetId, int RedRoseId, int WhiteRoseId, int WrapPaperId, int RibbonId, int WarehouseId)> SeedBouquetTestDataAsync(ApplicationDbContext context)
        {
            var catFlowers = new Category { Name = "Fresh Flowers", IsActive = true };
            var catPackaging = new Category { Name = "Packaging Materials", IsActive = true };
            var catBouquets = new Category { Name = "Bouquets", IsActive = true };

            var uomStem = new UnitOfMeasure { Name = "Stem", Symbol = "stm", AllowDecimals = false };
            var uomSheet = new UnitOfMeasure { Name = "Sheet", Symbol = "sht", AllowDecimals = true };
            var uomMeter = new UnitOfMeasure { Name = "Meter", Symbol = "m", AllowDecimals = true };
            var uomPiece = new UnitOfMeasure { Name = "Piece", Symbol = "pc", AllowDecimals = false };

            var warehouse = new Warehouse { Code = "MAIN", Name = "Main Warehouse", IsDefault = true, IsActive = true };

            context.Categories.AddRange(catFlowers, catPackaging, catBouquets);
            context.UnitsOfMeasure.AddRange(uomStem, uomSheet, uomMeter, uomPiece);
            context.Warehouses.Add(warehouse);
            await context.SaveChangesAsync();

            var redRose = new Product
            {
                SKU = "FLW-ROSE-RED",
                Name = "Red Naomi Rose",
                CategoryId = catFlowers.Id,
                UnitOfMeasureId = uomStem.Id,
                UnitPrice = 4.00m,
                CurrentAverageCost = 2.00m,
                ProductType = ProductType.FreshFlower,
                IsActive = true
            };

            var whiteRose = new Product
            {
                SKU = "FLW-ROSE-WHT",
                Name = "Avalanche White Rose",
                CategoryId = catFlowers.Id,
                UnitOfMeasureId = uomStem.Id,
                UnitPrice = 4.50m,
                CurrentAverageCost = 2.50m,
                ProductType = ProductType.FreshFlower,
                IsActive = true
            };

            var wrapPaper = new Product
            {
                SKU = "PKG-WRAP-KRAFT",
                Name = "Kraft Wrapping Paper",
                CategoryId = catPackaging.Id,
                UnitOfMeasureId = uomSheet.Id,
                UnitPrice = 2.50m,
                CurrentAverageCost = 1.50m,
                ProductType = ProductType.WrappingMaterial,
                IsActive = true
            };

            var ribbon = new Product
            {
                SKU = "PKG-RIB-SATIN",
                Name = "Satin Ribbon",
                CategoryId = catPackaging.Id,
                UnitOfMeasureId = uomMeter.Id,
                UnitPrice = 3.00m,
                CurrentAverageCost = 2.00m,
                ProductType = ProductType.Accessory,
                IsActive = true
            };

            var bouquet = new Product
            {
                SKU = "BQT-CLASSIC-ROSE",
                Name = "Classic Rose Bouquet",
                CategoryId = catBouquets.Id,
                UnitOfMeasureId = uomPiece.Id,
                UnitPrice = 55.00m,
                CurrentAverageCost = 0.00m,
                ProductType = ProductType.Bouquet,
                IsActive = true
            };

            context.Products.AddRange(redRose, whiteRose, wrapPaper, ribbon, bouquet);
            await context.SaveChangesAsync();

            return (bouquet.Id, redRose.Id, whiteRose.Id, wrapPaper.Id, ribbon.Id, warehouse.Id);
        }

        [Fact]
        public async Task RecipeCreation_And_CostCalculation_SucceedsWithSnapshots()
        {
            using var context = new ApplicationDbContext(_options);
            var (bouquetId, redRoseId, whiteRoseId, wrapId, _, _) = await SeedBouquetTestDataAsync(context);
            var service = new BouquetService(context);

            var model = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquetId,
                LaborCost = 5.00m,
                PackagingCost = 2.00m,
                Instructions = "Spiral arrangement, outer kraft paper wrap",
                SnapshotNotes = "Initial recipe definition",
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = redRoseId, QuantityRequired = 5, WasteMarginPercentage = 0 },
                    new RecipeItemFormViewModel { ComponentProductId = whiteRoseId, QuantityRequired = 3, WasteMarginPercentage = 0 },
                    new RecipeItemFormViewModel { ComponentProductId = wrapId, QuantityRequired = 1, WasteMarginPercentage = 0 }
                }
            };

            var (success, error, recipeId) = await service.SaveRecipeAsync(model, "user-admin");

            Assert.True(success, error);
            Assert.True(recipeId > 0);

            // Cost verification:
            // 5 Red Roses * $2.00 = $10.00
            // 3 White Roses * $2.50 = $7.50
            // 1 Wrap Paper * $1.50 = $1.50
            // Total components = $19.00
            // Total unit cost = $19.00 + $5.00 (labor) + $2.00 (packaging) = $26.00
            var bouquetProduct = await context.Products.FindAsync(bouquetId);
            Assert.NotNull(bouquetProduct);
            Assert.Equal(26.00m, bouquetProduct.CurrentAverageCost);

            // Snapshot verification
            var snapshots = await service.GetRecipeSnapshotsAsync(bouquetId);
            Assert.Single(snapshots);
            var snap = snapshots.First();
            Assert.Equal(1, snap.Version);
            Assert.Equal(26.00m, snap.TotalUnitCost);
            Assert.Equal(19.00m, snap.ComponentCostTotal);
            Assert.Equal(5.00m, snap.LaborCost);
            Assert.Equal(2.00m, snap.PackagingCost);
            Assert.Contains("FLW-ROSE-RED", snap.ItemsSnapshotJson);
        }

        [Fact]
        public async Task RecipeEditing_DoesNotRewriteHistoricalSnapshots()
        {
            using var context = new ApplicationDbContext(_options);
            var (bouquetId, redRoseId, whiteRoseId, wrapId, _, _) = await SeedBouquetTestDataAsync(context);
            var service = new BouquetService(context);

            // 1. Create Version 1 (5 Red Roses, 3 White, 1 Wrap -> Cost = $26.00)
            var modelV1 = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquetId,
                LaborCost = 5.00m,
                PackagingCost = 2.00m,
                SnapshotNotes = "Version 1 release",
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = redRoseId, QuantityRequired = 5, WasteMarginPercentage = 0 },
                    new RecipeItemFormViewModel { ComponentProductId = whiteRoseId, QuantityRequired = 3, WasteMarginPercentage = 0 },
                    new RecipeItemFormViewModel { ComponentProductId = wrapId, QuantityRequired = 1, WasteMarginPercentage = 0 }
                }
            };
            var (resV1, _, _) = await service.SaveRecipeAsync(modelV1, "user-admin");
            Assert.True(resV1);

            // 2. Edit to Version 2: Increase Red Roses from 5 to 8 (8 * $2 = 16 -> components = $25.00, total = $32.00)
            var modelV2 = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquetId,
                LaborCost = 5.00m,
                PackagingCost = 2.00m,
                SnapshotNotes = "Version 2 release: added 3 extra roses",
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = redRoseId, QuantityRequired = 8, WasteMarginPercentage = 0 },
                    new RecipeItemFormViewModel { ComponentProductId = whiteRoseId, QuantityRequired = 3, WasteMarginPercentage = 0 },
                    new RecipeItemFormViewModel { ComponentProductId = wrapId, QuantityRequired = 1, WasteMarginPercentage = 0 }
                }
            };
            var (resV2, _, _) = await service.SaveRecipeAsync(modelV2, "user-admin");
            Assert.True(resV2);

            // Verify both snapshots exist and Version 1 was NOT mutated
            var snapshots = await service.GetRecipeSnapshotsAsync(bouquetId);
            Assert.Equal(2, snapshots.Count);

            var v1Snap = snapshots.First(s => s.Version == 1);
            Assert.Equal(26.00m, v1Snap.TotalUnitCost);
            Assert.Equal(19.00m, v1Snap.ComponentCostTotal);
            Assert.Equal("Version 1 release", v1Snap.Notes);

            var v2Snap = snapshots.First(s => s.Version == 2);
            Assert.Equal(32.00m, v2Snap.TotalUnitCost);
            Assert.Equal(25.00m, v2Snap.ComponentCostTotal);
            Assert.Equal("Version 2 release: added 3 extra roses", v2Snap.Notes);
        }

        [Fact]
        public async Task CalculateRequirements_ForRequestedQuantity_ScalesCorrectly()
        {
            // Prompt example: A bouquet requires 5 red rose stems, 3 white rose stems, and 1 wrapping paper unit.
            // A request for 2 bouquets requires 10 red rose stems, 6 white rose stems, and 2 wrapping paper units.
            using var context = new ApplicationDbContext(_options);
            var (bouquetId, redRoseId, whiteRoseId, wrapId, _, _) = await SeedBouquetTestDataAsync(context);
            var service = new BouquetService(context);

            var model = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquetId,
                LaborCost = 5.00m,
                PackagingCost = 2.00m,
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = redRoseId, QuantityRequired = 5, WasteMarginPercentage = 0 },
                    new RecipeItemFormViewModel { ComponentProductId = whiteRoseId, QuantityRequired = 3, WasteMarginPercentage = 0 },
                    new RecipeItemFormViewModel { ComponentProductId = wrapId, QuantityRequired = 1, WasteMarginPercentage = 0 }
                }
            };
            await service.SaveRecipeAsync(model, "user-admin");

            // Calculate requirements for 2 bouquets
            var result = await service.CalculateRequirementsAsync(bouquetId, 2m);

            Assert.NotNull(result);
            Assert.Equal(2m, result.RequestedBouquetQuantity);

            var redRoses = result.Components.First(c => c.ComponentProductId == redRoseId);
            Assert.Equal(5m, redRoses.QuantityPerBouquet);
            Assert.Equal(10m, redRoses.TotalQuantityRequired);
            Assert.Equal(20.00m, redRoses.TotalComponentCost);

            var whiteRoses = result.Components.First(c => c.ComponentProductId == whiteRoseId);
            Assert.Equal(3m, whiteRoses.QuantityPerBouquet);
            Assert.Equal(6m, whiteRoses.TotalQuantityRequired);
            Assert.Equal(15.00m, whiteRoses.TotalComponentCost);

            var wrapPaper = result.Components.First(c => c.ComponentProductId == wrapId);
            Assert.Equal(1m, wrapPaper.QuantityPerBouquet);
            Assert.Equal(2m, wrapPaper.TotalQuantityRequired);
            Assert.Equal(3.00m, wrapPaper.TotalComponentCost);

            Assert.Equal(10.00m, result.TotalLaborCost); // $5 * 2
            Assert.Equal(4.00m, result.TotalPackagingCost); // $2 * 2
            Assert.Equal(38.00m, result.TotalComponentsCost); // $19 * 2
            Assert.Equal(52.00m, result.TotalEstimatedCost); // $26 * 2
        }

        [Fact]
        public async Task FractionalComponents_And_WasteMargins_HandledAccurately()
        {
            using var context = new ApplicationDbContext(_options);
            var (bouquetId, redRoseId, _, wrapId, ribbonId, _) = await SeedBouquetTestDataAsync(context);
            var service = new BouquetService(context);

            // Ribbon: 0.75m required with 10% waste margin -> effective 0.825m per bouquet
            // Wrap: 1.5 sheets required with 20% waste margin -> effective 1.8 sheets per bouquet
            // Roses: 6 stems required with 0% waste margin -> effective 6 stems per bouquet
            var model = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquetId,
                LaborCost = 4.00m,
                PackagingCost = 1.00m,
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = ribbonId, QuantityRequired = 0.75m, WasteMarginPercentage = 10m },
                    new RecipeItemFormViewModel { ComponentProductId = wrapId, QuantityRequired = 1.5m, WasteMarginPercentage = 20m },
                    new RecipeItemFormViewModel { ComponentProductId = redRoseId, QuantityRequired = 6m, WasteMarginPercentage = 0m }
                }
            };
            var (success, _, _) = await service.SaveRecipeAsync(model, "user-admin");
            Assert.True(success);

            // Calculate for 4 bouquets
            var result = await service.CalculateRequirementsAsync(bouquetId, 4m);
            Assert.NotNull(result);

            var ribbonComp = result.Components.First(c => c.ComponentProductId == ribbonId);
            Assert.Equal(0.75m, ribbonComp.QuantityPerBouquet);
            Assert.Equal(10m, ribbonComp.WasteMarginPercentage);
            Assert.Equal(0.825m, ribbonComp.EffectiveQuantityPerBouquet);
            Assert.Equal(3.3m, ribbonComp.TotalQuantityRequired); // 0.825 * 4 = 3.3m

            var wrapComp = result.Components.First(c => c.ComponentProductId == wrapId);
            Assert.Equal(1.5m, wrapComp.QuantityPerBouquet);
            Assert.Equal(20m, wrapComp.WasteMarginPercentage);
            Assert.Equal(1.8m, wrapComp.EffectiveQuantityPerBouquet);
            Assert.Equal(7.2m, wrapComp.TotalQuantityRequired); // 1.8 * 4 = 7.2 sheets

            var roseComp = result.Components.First(c => c.ComponentProductId == redRoseId);
            Assert.Equal(24m, roseComp.TotalQuantityRequired); // 6 * 4 = 24 stems
        }

        [Fact]
        public async Task CircularRecipe_Rejection_DirectSelfReference_IsPrevented()
        {
            using var context = new ApplicationDbContext(_options);
            var (bouquetId, redRoseId, _, _, _, _) = await SeedBouquetTestDataAsync(context);
            var service = new BouquetService(context);

            var model = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquetId,
                LaborCost = 0,
                PackagingCost = 0,
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = bouquetId, QuantityRequired = 1 } // Self-reference!
                }
            };

            var (success, error, _) = await service.SaveRecipeAsync(model, "user-admin");

            Assert.False(success);
            Assert.Contains("cannot contain itself", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CircularRecipe_Rejection_IndirectCycle_IsPrevented()
        {
            using var context = new ApplicationDbContext(_options);
            var (bouquetAId, redRoseId, _, _, _, _) = await SeedBouquetTestDataAsync(context);

            // Create a second bouquet B
            var bouquetB = new Product
            {
                SKU = "BQT-LUXURY",
                Name = "Luxury Deluxe Bouquet",
                CategoryId = 1,
                UnitOfMeasureId = 4,
                UnitPrice = 120.00m,
                CurrentAverageCost = 0m,
                ProductType = ProductType.Bouquet,
                IsActive = true
            };
            context.Products.Add(bouquetB);
            await context.SaveChangesAsync();

            var service = new BouquetService(context);

            // Step 1: Bouquet A contains Red Roses
            var recipeA = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquetAId,
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = redRoseId, QuantityRequired = 10 }
                }
            };
            var (resA, _, _) = await service.SaveRecipeAsync(recipeA, "user-admin");
            Assert.True(resA);

            // Step 2: Bouquet B contains Bouquet A as a sub-component
            var recipeB = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquetB.Id,
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = bouquetAId, QuantityRequired = 1 }
                }
            };
            var (resB, _, _) = await service.SaveRecipeAsync(recipeB, "user-admin");
            Assert.True(resB);

            // Step 3: Now edit Bouquet A to contain Bouquet B -> Cycles: A -> B -> A !
            var recipeACyclic = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquetAId,
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = bouquetB.Id, QuantityRequired = 1 }
                }
            };
            var (resCycle, cycleError, _) = await service.SaveRecipeAsync(recipeACyclic, "user-admin");

            Assert.False(resCycle);
            Assert.Contains("Circular recipe rejected", cycleError);
        }

        [Fact]
        public async Task SharedComponents_AcrossMultipleBouquets_WorkIndependently()
        {
            using var context = new ApplicationDbContext(_options);
            var (bouquet1Id, redRoseId, _, wrapId, _, _) = await SeedBouquetTestDataAsync(context);

            var bouquet2 = new Product
            {
                SKU = "BQT-MINI-ROSE",
                Name = "Mini Rose Trio",
                CategoryId = 1,
                UnitOfMeasureId = 4,
                UnitPrice = 25.00m,
                ProductType = ProductType.Bouquet,
                IsActive = true
            };
            context.Products.Add(bouquet2);
            await context.SaveChangesAsync();

            var service = new BouquetService(context);

            // Bouquet 1 uses Red Rose (5 stems) and Wrap (1 sheet)
            var b1Model = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquet1Id,
                LaborCost = 2.00m,
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = redRoseId, QuantityRequired = 5 },
                    new RecipeItemFormViewModel { ComponentProductId = wrapId, QuantityRequired = 1 }
                }
            };
            var (res1, _, _) = await service.SaveRecipeAsync(b1Model, "user-admin");
            Assert.True(res1);

            // Bouquet 2 also shares Red Rose (3 stems) and Wrap (0.5 sheets)
            var b2Model = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquet2.Id,
                LaborCost = 1.00m,
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = redRoseId, QuantityRequired = 3 },
                    new RecipeItemFormViewModel { ComponentProductId = wrapId, QuantityRequired = 0.5m }
                }
            };
            var (res2, _, _) = await service.SaveRecipeAsync(b2Model, "user-admin");
            Assert.True(res2);

            var calc1 = await service.CalculateRequirementsAsync(bouquet1Id, 1);
            var calc2 = await service.CalculateRequirementsAsync(bouquet2.Id, 1);

            Assert.NotNull(calc1);
            Assert.NotNull(calc2);
            Assert.Equal(5m, calc1.Components.First(c => c.ComponentProductId == redRoseId).TotalQuantityRequired);
            Assert.Equal(3m, calc2.Components.First(c => c.ComponentProductId == redRoseId).TotalQuantityRequired);
        }

        [Fact]
        public async Task ValidateAvailability_DetectsSufficiency_And_Shortages()
        {
            using var context = new ApplicationDbContext(_options);
            var (bouquetId, redRoseId, whiteRoseId, wrapId, _, warehouseId) = await SeedBouquetTestDataAsync(context);

            // Stock in warehouse:
            // Red Rose: 10 stems
            // White Rose: 6 stems
            // Wrap Paper: 1 sheet
            context.StockItems.AddRange(
                new StockItem { ProductId = redRoseId, WarehouseId = warehouseId, QuantityOnHand = 10 },
                new StockItem { ProductId = whiteRoseId, WarehouseId = warehouseId, QuantityOnHand = 6 },
                new StockItem { ProductId = wrapId, WarehouseId = warehouseId, QuantityOnHand = 1 }
            );
            await context.SaveChangesAsync();

            var service = new BouquetService(context);
            var model = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquetId,
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = redRoseId, QuantityRequired = 5 },
                    new RecipeItemFormViewModel { ComponentProductId = whiteRoseId, QuantityRequired = 3 },
                    new RecipeItemFormViewModel { ComponentProductId = wrapId, QuantityRequired = 1 }
                }
            };
            await service.SaveRecipeAsync(model, "user-admin");

            // Test 1: Requested quantity = 1 (Requires 5 red, 3 white, 1 wrap) -> SUFFICIENT
            var avail1 = await service.ValidateAvailabilityAsync(bouquetId, 1, warehouseId);
            Assert.NotNull(avail1);
            Assert.True(avail1.AllAvailable);
            Assert.All(avail1.Components, c => Assert.True(c.IsSufficient));

            // Test 2: Requested quantity = 2 (Requires 10 red, 6 white, 2 wraps) -> INSUFFICIENT (Wrap stock is only 1, short by 1)
            var avail2 = await service.ValidateAvailabilityAsync(bouquetId, 2, warehouseId);
            Assert.NotNull(avail2);
            Assert.False(avail2.AllAvailable);

            var wrapCheck = avail2.Components.First(c => c.ComponentProductId == wrapId);
            Assert.False(wrapCheck.IsSufficient);
            Assert.Equal(1m, wrapCheck.ShortageQuantity); // Needed 2, had 1 -> Shortage 1
        }

        [Fact]
        public async Task BouquetAssembly_Workflow_ConvertsComponentsToBouquets_WithoutDoubleCounting()
        {
            using var context = new ApplicationDbContext(_options);
            var (bouquetId, redRoseId, whiteRoseId, wrapId, _, warehouseId) = await SeedBouquetTestDataAsync(context);

            // Seed inventory: 50 red roses, 30 white roses, 10 wrapping sheets
            context.StockItems.AddRange(
                new StockItem { ProductId = redRoseId, WarehouseId = warehouseId, QuantityOnHand = 50 },
                new StockItem { ProductId = whiteRoseId, WarehouseId = warehouseId, QuantityOnHand = 30 },
                new StockItem { ProductId = wrapId, WarehouseId = warehouseId, QuantityOnHand = 10 }
            );
            await context.SaveChangesAsync();

            var service = new BouquetService(context);
            var model = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquetId,
                LaborCost = 5.00m,
                PackagingCost = 2.00m,
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = redRoseId, QuantityRequired = 5 },
                    new RecipeItemFormViewModel { ComponentProductId = whiteRoseId, QuantityRequired = 3 },
                    new RecipeItemFormViewModel { ComponentProductId = wrapId, QuantityRequired = 1 }
                }
            };
            await service.SaveRecipeAsync(model, "user-admin");

            // Assemble 2 bouquets
            var token = Guid.NewGuid().ToString();
            var assembleRequest = new AssembleBouquetsViewModel
            {
                BouquetProductId = bouquetId,
                WarehouseId = warehouseId,
                QuantityToAssemble = 2,
                Notes = "Assembly run #1",
                IdempotencyToken = token
            };

            var (success, error) = await service.AssembleBouquetsAsync(assembleRequest, "user-clerk");
            Assert.True(success, error);

            // Verify Stock balances:
            // Red Rose: 50 - 10 = 40
            var redRoseStock = await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(40m, redRoseStock.QuantityOnHand);

            // White Rose: 30 - 6 = 24
            var whiteRoseStock = await context.StockItems.FirstAsync(s => s.ProductId == whiteRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(24m, whiteRoseStock.QuantityOnHand);

            // Wrapping Paper: 10 - 2 = 8
            var wrapStock = await context.StockItems.FirstAsync(s => s.ProductId == wrapId && s.WarehouseId == warehouseId);
            Assert.Equal(8m, wrapStock.QuantityOnHand);

            // Finished Bouquet stock: 0 + 2 = 2
            var bouquetStock = await context.StockItems.FirstAsync(s => s.ProductId == bouquetId && s.WarehouseId == warehouseId);
            Assert.Equal(2m, bouquetStock.QuantityOnHand);

            // Verify StockMovements ledger:
            var movements = await context.StockMovements.Where(m => m.ReferenceId == token).ToListAsync();
            Assert.Equal(4, movements.Count);

            // 3 BouquetAssemblyOut movements
            var outMovements = movements.Where(m => m.MovementType == MovementType.BouquetAssemblyOut).ToList();
            Assert.Equal(3, outMovements.Count);
            Assert.Equal(-10m, outMovements.First(m => m.ProductId == redRoseId).Quantity);
            Assert.Equal(-6m, outMovements.First(m => m.ProductId == whiteRoseId).Quantity);
            Assert.Equal(-2m, outMovements.First(m => m.ProductId == wrapId).Quantity);

            // 1 BouquetAssemblyIn movement
            var inMovement = movements.First(m => m.MovementType == MovementType.BouquetAssemblyIn);
            Assert.Equal(2m, inMovement.Quantity);
            Assert.Equal(26.00m, inMovement.UnitCost);

            // Test Idempotency: Repeating same assembly request returns success without double-decrementing
            var (idemSuccess, _) = await service.AssembleBouquetsAsync(assembleRequest, "user-clerk");
            Assert.True(idemSuccess);

            // Stock remains exactly 40, 24, 8, 2
            var redAfter = await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(40m, redAfter.QuantityOnHand);
        }

        [Fact]
        public async Task BouquetAssembly_FailsAndRollsBack_WhenInsufficientComponentStock()
        {
            using var context = new ApplicationDbContext(_options);
            var (bouquetId, redRoseId, whiteRoseId, wrapId, _, warehouseId) = await SeedBouquetTestDataAsync(context);

            // Seed only 5 red roses (enough for only 1 bouquet)
            context.StockItems.AddRange(
                new StockItem { ProductId = redRoseId, WarehouseId = warehouseId, QuantityOnHand = 5 },
                new StockItem { ProductId = whiteRoseId, WarehouseId = warehouseId, QuantityOnHand = 20 },
                new StockItem { ProductId = wrapId, WarehouseId = warehouseId, QuantityOnHand = 10 }
            );
            await context.SaveChangesAsync();

            var service = new BouquetService(context);
            var model = new BouquetRecipeFormViewModel
            {
                BouquetProductId = bouquetId,
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = redRoseId, QuantityRequired = 5 },
                    new RecipeItemFormViewModel { ComponentProductId = whiteRoseId, QuantityRequired = 3 },
                    new RecipeItemFormViewModel { ComponentProductId = wrapId, QuantityRequired = 1 }
                }
            };
            await service.SaveRecipeAsync(model, "user-admin");

            // Attempt to assemble 3 bouquets (requires 15 red roses, but only 5 available)
            var assembleRequest = new AssembleBouquetsViewModel
            {
                BouquetProductId = bouquetId,
                WarehouseId = warehouseId,
                QuantityToAssemble = 3,
                IdempotencyToken = Guid.NewGuid().ToString()
            };

            var (success, error) = await service.AssembleBouquetsAsync(assembleRequest, "user-clerk");

            Assert.False(success);
            Assert.Contains("Insufficient component stock", error);

            // Verify stock was untouched
            var redStock = await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(5m, redStock.QuantityOnHand);

            // No bouquet stock created
            var bStock = await context.StockItems.FirstOrDefaultAsync(s => s.ProductId == bouquetId && s.WarehouseId == warehouseId);
            Assert.Null(bStock);
        }

        [Fact]
        public async Task RegularProduct_DoesNotRequireRecipe_AndNonBouquetCannotHaveRecipe()
        {
            using var context = new ApplicationDbContext(_options);
            var (_, redRoseId, whiteRoseId, _, _, _) = await SeedBouquetTestDataAsync(context);
            var service = new BouquetService(context);

            // Attempt to save recipe for a FreshFlower (not a Bouquet)
            var invalidModel = new BouquetRecipeFormViewModel
            {
                BouquetProductId = redRoseId, // ProductType == FreshFlower
                Items = new List<RecipeItemFormViewModel>
                {
                    new RecipeItemFormViewModel { ComponentProductId = whiteRoseId, QuantityRequired = 1 }
                }
            };

            var (success, error, _) = await service.SaveRecipeAsync(invalidModel, "user-admin");

            Assert.False(success);
            Assert.Contains("Only products classified as 'Bouquet' can have bouquet recipes", error);
        }
    }
}
