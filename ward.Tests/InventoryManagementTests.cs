using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Entities.Inventory;
using ward.Models.Inventory;
using ward.Services.Inventory;
using Xunit;

namespace ward.Tests
{
    public class InventoryManagementTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public InventoryManagementTests()
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

        private async Task<(int ProductId, int Warehouse1Id, int Warehouse2Id)> SeedInventoryTestDataAsync(ApplicationDbContext context)
        {
            var category = new Category { Name = "Fresh Roses", IsActive = true };
            var uom = new UnitOfMeasure { Name = "Stem", Symbol = "stm", AllowDecimals = false };
            var w1 = new Warehouse { Code = "MAIN", Name = "Main Floor", IsDefault = true, IsActive = true };
            var w2 = new Warehouse { Code = "COLD", Name = "Cold Storage", IsDefault = false, IsActive = true };

            context.Categories.Add(category);
            context.UnitsOfMeasure.Add(uom);
            context.Warehouses.AddRange(w1, w2);
            await context.SaveChangesAsync();

            var product = new Product
            {
                SKU = "ROSE-RED",
                Name = "Red Naomi Rose",
                CategoryId = category.Id,
                UnitOfMeasureId = uom.Id,
                UnitPrice = 15.00m,
                CurrentAverageCost = 5.00m,
                MinimumStockLevel = 20.0000m,
                ProductType = ProductType.FreshFlower,
                IsActive = true
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            return (product.Id, w1.Id, w2.Id);
        }

        // ==================== 1. Receipt & Weighted Average Cost ====================

        [Fact]
        public async Task ReceiveStock_UpdatesStockBalance_AndCalculatesWeightedAverageCost()
        {
            using var context = new ApplicationDbContext(_options);
            var service = new InventoryService(context);
            var (productId, w1Id, _) = await SeedInventoryTestDataAsync(context);

            // 1. Initial receipt: 100 stems at $5.00
            var r1 = await service.ReceiveStockAsync(new ReceiveStockViewModel
            {
                ProductId = productId,
                WarehouseId = w1Id,
                Quantity = 100m,
                UnitCost = 5.00m,
                ReferenceNumber = "PO-001",
                IdempotencyToken = "IDEMP-REC-1"
            }, "TestClerk");

            Assert.True(r1.Success, r1.ErrorMessage);
            Assert.Equal(100m, r1.NewQuantityOnHand);

            // 2. Second receipt: 50 stems at $8.00
            // Expected new average cost: ((100 * 5) + (50 * 8)) / 150 = (500 + 400) / 150 = 900 / 150 = 6.00
            var r2 = await service.ReceiveStockAsync(new ReceiveStockViewModel
            {
                ProductId = productId,
                WarehouseId = w1Id,
                Quantity = 50m,
                UnitCost = 8.00m,
                ReferenceNumber = "PO-002",
                IdempotencyToken = "IDEMP-REC-2"
            }, "TestClerk");

            Assert.True(r2.Success);
            Assert.Equal(150m, r2.NewQuantityOnHand);

            var updatedProduct = await context.Products.FindAsync(productId);
            Assert.NotNull(updatedProduct);
            Assert.Equal(6.00m, updatedProduct.CurrentAverageCost);
        }

        // ==================== 2. Stock Balance vs Movement Ledger Integrity ====================

        [Fact]
        public async Task StockIntegrity_StockOnHandEqualsSumOfMovements()
        {
            using var context = new ApplicationDbContext(_options);
            var service = new InventoryService(context);
            var (productId, w1Id, w2Id) = await SeedInventoryTestDataAsync(context);

            // Receive 100 in W1
            await service.ReceiveStockAsync(new ReceiveStockViewModel
            {
                ProductId = productId,
                WarehouseId = w1Id,
                Quantity = 100m,
                UnitCost = 5m,
                ReferenceNumber = "REC-1",
                IdempotencyToken = "TOK-1"
            }, "Clerk");

            // Transfer 30 from W1 to W2
            await service.TransferStockAsync(new TransferStockViewModel
            {
                ProductId = productId,
                SourceWarehouseId = w1Id,
                DestinationWarehouseId = w2Id,
                Quantity = 30m,
                IdempotencyToken = "TOK-2"
            }, "Clerk");

            // Issue 5 from W1 (wilted)
            await service.IssueStockAsync(new IssueStockViewModel
            {
                ProductId = productId,
                WarehouseId = w1Id,
                Quantity = 5m,
                ReasonType = MovementType.DamagedDiscardOut,
                Notes = "Wilted stems",
                IdempotencyToken = "TOK-3"
            }, "Clerk");

            // Adjust W2 from 30 to 32 (found 2 more)
            await service.AdjustStockAsync(new AdjustStockViewModel
            {
                ProductId = productId,
                WarehouseId = w2Id,
                NewQuantityOnHand = 32m,
                Reason = "Physical stocktaking",
                IdempotencyToken = "TOK-4"
            }, "Clerk");

            // Verify W1: 100 - 30 - 5 = 65
            var w1Stock = await service.GetProductStockInWarehouseAsync(productId, w1Id);
            Assert.Equal(65m, w1Stock);
            Assert.True(await service.VerifyStockIntegrityAsync(productId, w1Id));

            // Verify W2: 30 + 2 (adjustment) = 32
            var w2Stock = await service.GetProductStockInWarehouseAsync(productId, w2Id);
            Assert.Equal(32m, w2Stock);
            Assert.True(await service.VerifyStockIntegrityAsync(productId, w2Id));

            // Verify total stock across all warehouses: 65 + 32 = 97
            Assert.Equal(97m, await service.GetProductTotalStockAsync(productId));
        }

        // ==================== 3. Inter-Warehouse Transfer ====================

        [Fact]
        public async Task TransferStock_ReducesSource_IncreasesDestination_Atomically()
        {
            using var context = new ApplicationDbContext(_options);
            var service = new InventoryService(context);
            var (productId, w1Id, w2Id) = await SeedInventoryTestDataAsync(context);

            // Seed 80 in W1
            await service.ReceiveStockAsync(new ReceiveStockViewModel
            {
                ProductId = productId,
                WarehouseId = w1Id,
                Quantity = 80m,
                UnitCost = 5m,
                ReferenceNumber = "INIT",
                IdempotencyToken = "INIT-TOKEN"
            }, "Clerk");

            // Transfer 25 from W1 to W2
            var result = await service.TransferStockAsync(new TransferStockViewModel
            {
                ProductId = productId,
                SourceWarehouseId = w1Id,
                DestinationWarehouseId = w2Id,
                Quantity = 25m,
                Notes = "Routine shelf replenishment",
                IdempotencyToken = "TRF-8899"
            }, "Clerk");

            Assert.True(result.Success);
            Assert.Equal(55m, result.NewQuantityOnHand); // Source balance

            var destStock = await service.GetProductStockInWarehouseAsync(productId, w2Id);
            Assert.Equal(25m, destStock);

            // Verify Movements
            var movements = await context.StockMovements
                .Where(m => m.ReferenceType == "Transfer")
                .ToListAsync();

            Assert.Equal(2, movements.Count);
            var outM = movements.First(m => m.Quantity < 0);
            var inM = movements.First(m => m.Quantity > 0);

            Assert.Equal(-25m, outM.Quantity);
            Assert.Equal(w1Id, outM.WarehouseId);
            Assert.Equal(25m, inM.Quantity);
            Assert.Equal(w2Id, inM.WarehouseId);
        }

        // ==================== 4. Insufficient Stock & Negative Prevention ====================

        [Fact]
        public async Task IssueStock_And_TransferStock_RejectInsufficientStock_AndPreventNegativeInventory()
        {
            using var context = new ApplicationDbContext(_options);
            var service = new InventoryService(context);
            var (productId, w1Id, w2Id) = await SeedInventoryTestDataAsync(context);

            // Stock 10 in W1
            await service.ReceiveStockAsync(new ReceiveStockViewModel
            {
                ProductId = productId,
                WarehouseId = w1Id,
                Quantity = 10m,
                UnitCost = 5m,
                ReferenceNumber = "SEED",
                IdempotencyToken = "S-1"
            }, "Clerk");

            // Attempt to issue 15 (exceeds available 10)
            var issueResult = await service.IssueStockAsync(new IssueStockViewModel
            {
                ProductId = productId,
                WarehouseId = w1Id,
                Quantity = 15m,
                ReasonType = MovementType.DamagedDiscardOut,
                Notes = "Discard attempt",
                IdempotencyToken = "ISSUE-OVER"
            }, "Clerk");

            Assert.False(issueResult.Success);
            Assert.Contains("Insufficient stock", issueResult.ErrorMessage);

            // Attempt to transfer 15 from W1 to W2
            var transferResult = await service.TransferStockAsync(new TransferStockViewModel
            {
                ProductId = productId,
                SourceWarehouseId = w1Id,
                DestinationWarehouseId = w2Id,
                Quantity = 15m,
                IdempotencyToken = "TRF-OVER"
            }, "Clerk");

            Assert.False(transferResult.Success);
            Assert.Contains("Insufficient stock", transferResult.ErrorMessage);

            // Verify stock remained unchanged at 10
            var finalStock = await service.GetProductStockInWarehouseAsync(productId, w1Id);
            Assert.Equal(10m, finalStock);
        }

        // ==================== 5. Duplicate Submission / Idempotency ====================

        [Fact]
        public async Task Idempotency_DuplicateSubmissions_DoNotDuplicateMovementsOrAlterStockTwice()
        {
            using var context = new ApplicationDbContext(_options);
            var service = new InventoryService(context);
            var (productId, w1Id, _) = await SeedInventoryTestDataAsync(context);

            var receiveRequest = new ReceiveStockViewModel
            {
                ProductId = productId,
                WarehouseId = w1Id,
                Quantity = 50m,
                UnitCost = 5m,
                ReferenceNumber = "PO-DUPLICATE-TEST",
                IdempotencyToken = "SAME-IDEMPOTENCY-KEY"
            };

            // First submission
            var res1 = await service.ReceiveStockAsync(receiveRequest, "Clerk");
            Assert.True(res1.Success);
            Assert.Equal(50m, res1.NewQuantityOnHand);

            // Immediate duplicate submission (e.g. double click or network retry)
            var res2 = await service.ReceiveStockAsync(receiveRequest, "Clerk");
            Assert.True(res2.Success);
            Assert.Equal(50m, res2.NewQuantityOnHand); // Stock must remain 50, NOT 100!

            var movementCount = await context.StockMovements
                .CountAsync(m => m.ReferenceId == "SAME-IDEMPOTENCY-KEY");
            Assert.Equal(1, movementCount);

            var stockOnHand = await service.GetProductStockInWarehouseAsync(productId, w1Id);
            Assert.Equal(50m, stockOnHand);
        }

        // ==================== 6. Warehouse CRUD & Uniqueness ====================

        [Fact]
        public async Task WarehouseService_PreventsDuplicateCode_AndHandlesDefaultToggles()
        {
            using var context = new ApplicationDbContext(_options);
            var service = new WarehouseService(context);

            var (productId, w1Id, _) = await SeedInventoryTestDataAsync(context);

            // 1. Attempt Duplicate Code
            var dupResult = await service.CreateAsync(new WarehouseFormViewModel
            {
                Code = "main", // duplicate of MAIN
                Name = "Another Main"
            });
            Assert.False(dupResult.Success);
            Assert.Contains("already exists", dupResult.Error);

            // 2. Create new default warehouse and verify old default is unset
            var createResult = await service.CreateAsync(new WarehouseFormViewModel
            {
                Code = "SHOP-FRONT",
                Name = "Shop Front Retail",
                IsDefault = true,
                IsActive = true
            });
            Assert.True(createResult.Success);

            var w1 = await context.Warehouses.FindAsync(w1Id);
            Assert.False(w1!.IsDefault); // Old default was cleared

            var newDefault = await context.Warehouses.FirstAsync(w => w.Code == "SHOP-FRONT");
            Assert.True(newDefault.IsDefault);

            // 3. Prevent deactivating default warehouse
            var toggleResult = await service.ToggleActiveAsync(newDefault.Id);
            Assert.False(toggleResult.Success);
            Assert.Contains("Cannot deactivate the default warehouse", toggleResult.Error);
        }

        // ==================== 7. Transaction Rollback on Failure ====================

        [Fact]
        public async Task TransactionRollback_OnFailure_LeavesStockAndMovementsUnchanged()
        {
            using var context = new ApplicationDbContext(_options);
            var service = new InventoryService(context);
            var (productId, w1Id, _) = await SeedInventoryTestDataAsync(context);

            // Initial stock = 50
            await service.ReceiveStockAsync(new ReceiveStockViewModel
            {
                ProductId = productId,
                WarehouseId = w1Id,
                Quantity = 50m,
                UnitCost = 5m,
                ReferenceNumber = "ROLLBACK-INIT",
                IdempotencyToken = "RB-0"
            }, "Clerk");

            // Attempt a transfer to an invalid/non-existent destination warehouse (Id = 99999)
            var failTransfer = await service.TransferStockAsync(new TransferStockViewModel
            {
                ProductId = productId,
                SourceWarehouseId = w1Id,
                DestinationWarehouseId = 99999, // Non-existent
                Quantity = 20m,
                IdempotencyToken = "RB-FAIL-TRF"
            }, "Clerk");

            Assert.False(failTransfer.Success);
            Assert.Contains("Destination warehouse not found", failTransfer.ErrorMessage);

            // Verify source stock remained at 50 (no partial deduction committed)
            var sourceStock = await service.GetProductStockInWarehouseAsync(productId, w1Id);
            Assert.Equal(50m, sourceStock);

            // Verify no transfer movements were committed
            var transferMovementsCount = await context.StockMovements
                .CountAsync(m => m.ReferenceType == "Transfer");
            Assert.Equal(0, transferMovementsCount);
        }

        // ==================== 8. Consecutive / Simultaneous Stock Operations ====================

        [Fact]
        public async Task SimultaneousOperations_ConsecutiveDeductions_EnforcesAvailableQuantity()
        {
            using var context = new ApplicationDbContext(_options);
            var service = new InventoryService(context);
            var (productId, w1Id, _) = await SeedInventoryTestDataAsync(context);

            // Seed 30 units
            await service.ReceiveStockAsync(new ReceiveStockViewModel
            {
                ProductId = productId,
                WarehouseId = w1Id,
                Quantity = 30m,
                UnitCost = 5m,
                ReferenceNumber = "CONCURRENT-SEED",
                IdempotencyToken = "CONC-1"
            }, "Clerk");

            // Operation A: deduct 20 units
            var opA = await service.IssueStockAsync(new IssueStockViewModel
            {
                ProductId = productId,
                WarehouseId = w1Id,
                Quantity = 20m,
                ReasonType = MovementType.DamagedDiscardOut,
                Notes = "Deduction A",
                IdempotencyToken = "CONC-A"
            }, "UserA");

            Assert.True(opA.Success);
            Assert.Equal(10m, opA.NewQuantityOnHand);

            // Operation B: attempts to deduct 20 units (now only 10 available)
            var opB = await service.IssueStockAsync(new IssueStockViewModel
            {
                ProductId = productId,
                WarehouseId = w1Id,
                Quantity = 20m,
                ReasonType = MovementType.DamagedDiscardOut,
                Notes = "Deduction B",
                IdempotencyToken = "CONC-B"
            }, "UserB");

            Assert.False(opB.Success);
            Assert.Contains("Insufficient stock", opB.ErrorMessage);

            // Remaining stock is strictly 10
            var finalStock = await service.GetProductStockInWarehouseAsync(productId, w1Id);
            Assert.Equal(10m, finalStock);
        }
    }
}
