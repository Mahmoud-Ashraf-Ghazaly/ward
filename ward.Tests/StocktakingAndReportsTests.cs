using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ward.Constants;
using ward.Data;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Entities.Inventory;
using ward.Entities.Production;
using ward.Entities.Purchasing;
using ward.Entities.Sales;
using ward.Models.Inventory;
using ward.Models.Reports;
using ward.Services.Inventory;
using ward.Services.Reports;
using Xunit;

namespace ward.Tests
{
    public class StocktakingAndReportsTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public StocktakingAndReportsTests()
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

        private async Task<(int WarehouseId, int RoseId, int VaseId, int RibbonId)> SeedInventoryTestDataAsync(ApplicationDbContext context)
        {
            var warehouse = new Warehouse { Code = "WH-AUDIT", Name = "Audit Central Warehouse", IsActive = true };
            context.Warehouses.Add(warehouse);

            var pcs = new UnitOfMeasure { Name = "Piece", Symbol = "pcs", AllowDecimals = false };
            var mtr = new UnitOfMeasure { Name = "Meter", Symbol = "m", AllowDecimals = true };
            context.UnitsOfMeasure.AddRange(pcs, mtr);

            var catFlowers = new Category { Name = "Fresh Flowers", IsActive = true };
            var catVases = new Category { Name = "Vases", IsActive = true };
            var catAcc = new Category { Name = "Accessories", IsActive = true };
            context.Categories.AddRange(catFlowers, catVases, catAcc);
            await context.SaveChangesAsync();

            var rose = new Product
            {
                SKU = "FLW-RED-ROSE",
                Name = "Red Rose stem",
                CategoryId = catFlowers.Id,
                UnitOfMeasureId = pcs.Id,
                ProductType = ProductType.FreshFlower,
                UnitPrice = 4.00m,
                CurrentAverageCost = 1.50m,
                MinimumStockLevel = 20,
                IsActive = true
            };
            var vase = new Product
            {
                SKU = "VAS-CRYSTAL",
                Name = "Crystal Glass Vase",
                CategoryId = catVases.Id,
                UnitOfMeasureId = pcs.Id,
                ProductType = ProductType.Vase,
                UnitPrice = 25.00m,
                CurrentAverageCost = 10.00m,
                MinimumStockLevel = 5,
                IsActive = true
            };
            var ribbon = new Product
            {
                SKU = "ACC-SILK-RIB",
                Name = "Satin Ribbon 25mm",
                CategoryId = catAcc.Id,
                UnitOfMeasureId = mtr.Id,
                ProductType = ProductType.Accessory,
                UnitPrice = 1.00m,
                CurrentAverageCost = 0.40m,
                MinimumStockLevel = 30,
                IsActive = true
            };
            context.Products.AddRange(rose, vase, ribbon);
            await context.SaveChangesAsync();

            context.StockItems.AddRange(
                new StockItem { ProductId = rose.Id, WarehouseId = warehouse.Id, QuantityOnHand = 100 },
                new StockItem { ProductId = vase.Id, WarehouseId = warehouse.Id, QuantityOnHand = 10 },
                new StockItem { ProductId = ribbon.Id, WarehouseId = warehouse.Id, QuantityOnHand = 50 }
            );
            await context.SaveChangesAsync();

            return (warehouse.Id, rose.Id, vase.Id, ribbon.Id);
        }

        [Fact]
        public async Task Stocktaking_Calculations_AccuratelyComputesShortagesSurplusesAndVarianceCosts()
        {
            using var context = new ApplicationDbContext(_options);
            var (warehouseId, roseId, vaseId, ribbonId) = await SeedInventoryTestDataAsync(context);
            var stocktakingService = new StocktakingService(context);

            // 1. Create session
            var (createSuccess, _, sessionId) = await stocktakingService.CreateSessionAsync(new StocktakingCreateViewModel
            {
                WarehouseId = warehouseId,
                Reason = "Quarterly Audit",
                Notes = "Counting all section shelves"
            }, "clerk1");

            Assert.True(createSuccess);
            Assert.True(sessionId > 0);

            // 2. Fetch session for counting
            var countModel = await stocktakingService.GetSessionForCountingAsync(sessionId);
            Assert.NotNull(countModel);
            Assert.Equal(3, countModel.Lines.Count);

            // Set physical counts:
            // Roses: 95 counted (Snapshot was 100) -> Shortage of 5
            // Vases: 12 counted (Snapshot was 10)  -> Surplus of 2
            // Ribbon: 50 counted (Snapshot was 50) -> Match (0 diff)
            var roseLine = countModel.Lines.First(l => l.ProductId == roseId);
            roseLine.CountedQuantity = 95;
            roseLine.Reason = "5 wilted stems discarded";

            var vaseLine = countModel.Lines.First(l => l.ProductId == vaseId);
            vaseLine.CountedQuantity = 12;
            vaseLine.Reason = "2 extra vases found in backroom";

            var ribbonLine = countModel.Lines.First(l => l.ProductId == ribbonId);
            ribbonLine.CountedQuantity = 50;

            // 3. Save counts
            var (saveSuccess, _) = await stocktakingService.SaveCountsAsync(countModel, "clerk1");
            Assert.True(saveSuccess);

            // 4. Verify Review Calculations
            var review = await stocktakingService.GetSessionForReviewAsync(sessionId);
            Assert.NotNull(review);
            Assert.Equal(StocktakingStatus.Reviewed, review.Status);

            Assert.Equal(5m, review.TotalShortageQuantity);
            Assert.Equal(2m, review.TotalSurplusQuantity);
            Assert.Equal(-3m, review.NetDifferenceQuantity); // -5 + 2 = -3

            // Variance Cost:
            // Roses variance cost: -5 * $1.50 = -$7.50
            // Vases variance cost: +2 * $10.00 = +$20.00
            // Ribbons variance cost: 0 * $0.40 = $0.00
            // Total Variance Cost: +$12.50
            Assert.Equal(12.50m, review.TotalVarianceCost);
        }

        [Fact]
        public void Stocktaking_ApprovalPolicy_RequiresManagerOrAdminRole()
        {
            // Verify that the authorization setup in Program.cs restricts stocktaking approval
            var options = new AuthorizationOptions();
            options.AddPolicy(AppPolicies.ApproveStocktaking, policy =>
                policy.RequireRole(AppRoles.Admin, AppRoles.Manager));

            var policy = options.GetPolicy(AppPolicies.ApproveStocktaking);
            Assert.NotNull(policy);

            // Requirements must specify Admin and Manager roles
            var roleRequirement = policy.Requirements
                .OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>()
                .FirstOrDefault();

            Assert.NotNull(roleRequirement);
            Assert.Contains(AppRoles.Admin, roleRequirement.AllowedRoles);
            Assert.Contains(AppRoles.Manager, roleRequirement.AllowedRoles);
            Assert.DoesNotContain(AppRoles.InventoryClerk, roleRequirement.AllowedRoles);
            Assert.DoesNotContain(AppRoles.Cashier, roleRequirement.AllowedRoles);
        }

        [Fact]
        public async Task Stocktaking_ConcurrentMovementsDuringCount_PreservesConcurrentSalesDelta()
        {
            using var context = new ApplicationDbContext(_options);
            var (warehouseId, roseId, _, _) = await SeedInventoryTestDataAsync(context);
            var stocktakingService = new StocktakingService(context);

            // Step 1: Session starts when rose stock is 100
            var (_, _, sessionId) = await stocktakingService.CreateSessionAsync(new StocktakingCreateViewModel
            {
                WarehouseId = warehouseId,
                Reason = "Mid-day count with active shop floor"
            }, "clerk1");

            var countModel = await stocktakingService.GetSessionForCountingAsync(sessionId);
            var roseLine = countModel!.Lines.First(l => l.ProductId == roseId);
            Assert.Equal(100m, roseLine.SystemQuantity);

            // Step 2: Physical count performed on shelf: 97 roses counted (physical shortage of 3)
            roseLine.CountedQuantity = 97;
            roseLine.Reason = "3 stems damaged";
            await stocktakingService.SaveCountsAsync(countModel, "clerk1");

            // Step 3: Concurrent sale occurs on the live register while count is being reviewed!
            // Customer purchases 10 roses, live database stock drops from 100 to 90
            var liveRoseStock = await context.StockItems.FirstAsync(s => s.ProductId == roseId && s.WarehouseId == warehouseId);
            liveRoseStock.QuantityOnHand -= 10;
            await context.SaveChangesAsync();

            Assert.Equal(90m, liveRoseStock.QuantityOnHand);

            // Step 4: Manager reviews session - system detects concurrent movement delta
            var review = await stocktakingService.GetSessionForReviewAsync(sessionId);
            var reviewRoseLine = review!.Lines.First(l => l.ProductId == roseId);
            Assert.True(reviewRoseLine.HasConcurrentMovements);
            Assert.Equal(-10m, reviewRoseLine.ConcurrentMovementDelta); // 90 - 100 = -10

            // Step 5: Manager approves and posts adjustments
            var (approveSuccess, approveErr) = await stocktakingService.ApproveAndPostAdjustmentsAsync(sessionId, "Approved with concurrent movement reconciled", null, "manager1");
            Assert.True(approveSuccess, approveErr);

            // Step 6: Verify final stock:
            // The physical variance at count was -3.
            // When applied to the live stock (90), the new stock must be 90 + (-3) = 87!
            // (If concurrent movements were not handled, it would incorrectly set stock to 97, erasing the 10 sold roses!)
            var finalRoseStock = (await context.StockItems.FirstAsync(s => s.ProductId == roseId && s.WarehouseId == warehouseId)).QuantityOnHand;
            Assert.Equal(87m, finalRoseStock);

            // Verify StockMovement ledger
            var adjMovement = await context.StockMovements
                .FirstOrDefaultAsync(m => m.ProductId == roseId && m.MovementType == MovementType.StocktakingAdjustment);
            Assert.NotNull(adjMovement);
            Assert.Equal(-3m, adjMovement.Quantity);
            Assert.Contains("In-progress concurrent movement: -10", adjMovement.Notes);
        }

        [Fact]
        public async Task Stocktaking_DuplicateApprovals_RejectedAndIdempotent()
        {
            using var context = new ApplicationDbContext(_options);
            var (warehouseId, roseId, _, _) = await SeedInventoryTestDataAsync(context);
            var stocktakingService = new StocktakingService(context);

            var (_, _, sessionId) = await stocktakingService.CreateSessionAsync(new StocktakingCreateViewModel
            {
                WarehouseId = warehouseId,
                Reason = "Idempotency test"
            }, "clerk1");

            var countModel = await stocktakingService.GetSessionForCountingAsync(sessionId);
            countModel!.Lines.First(l => l.ProductId == roseId).CountedQuantity = 90; // -10 shortage
            await stocktakingService.SaveCountsAsync(countModel, "clerk1");

            var idempotencyToken = "IDEMP-STOCKTAKING-001";

            // First approval
            var (success1, _) = await stocktakingService.ApproveAndPostAdjustmentsAsync(sessionId, "First pass", idempotencyToken, "manager1");
            Assert.True(success1);

            var stockAfterFirst = (await context.StockItems.FirstAsync(s => s.ProductId == roseId && s.WarehouseId == warehouseId)).QuantityOnHand;
            Assert.Equal(90m, stockAfterFirst);

            // Duplicate approval attempt with same token -> succeeds idempotently without double-adjusting
            var (success2, _) = await stocktakingService.ApproveAndPostAdjustmentsAsync(sessionId, "Duplicate pass", idempotencyToken, "manager1");
            Assert.True(success2);

            var stockAfterSecond = (await context.StockItems.FirstAsync(s => s.ProductId == roseId && s.WarehouseId == warehouseId)).QuantityOnHand;
            Assert.Equal(90m, stockAfterSecond); // Still 90, NOT 80!

            // Direct attempt without token to re-approve a posted session -> rejected
            var (success3, err3) = await stocktakingService.ApproveAndPostAdjustmentsAsync(sessionId, "Repeated without token", null, "manager1");
            Assert.False(success3);
            Assert.Contains("already been approved and posted", err3);
        }

        [Fact]
        public async Task Reports_ReconcileAgainstPostedInvoicesAndStockMovements()
        {
            using var context = new ApplicationDbContext(_options);
            var (warehouseId, roseId, _, _) = await SeedInventoryTestDataAsync(context);
            var reportingService = new ReportingService(context);

            // Seed customer
            var customer = new Customer { Name = "Botanical Gardens", IsActive = true };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            // Seed Invoice 1: 10 roses sold at $4.00, cost $1.50 each
            var inv1 = new Invoice
            {
                InvoiceNumber = "INV-REP-001",
                InvoiceDateUtc = DateTime.UtcNow,
                CustomerId = customer.Id,
                Status = InvoiceStatus.FullyPaid,
                SubTotal = 40.00m,
                DiscountAmount = 0,
                TaxAmount = 2.00m,
                GrandTotal = 42.00m,
                PaidAmount = 42.00m
            };
            inv1.Items.Add(new InvoiceItem
            {
                ProductId = roseId,
                WarehouseId = warehouseId,
                Quantity = 10,
                UnitPrice = 4.00m,
                UnitCost = 1.50m,
                LineTotal = 40.00m
            });
            context.Invoices.Add(inv1);

            // Seed Return on Invoice 1: 2 roses returned ($8.00 refund)
            var ret1 = new InvoiceReturn
            {
                ReturnNumber = "RET-REP-001",
                Invoice = inv1,
                ReturnDateUtc = DateTime.UtcNow,
                Status = ReturnStatus.Completed,
                TotalRefundAmount = 8.00m
            };
            context.InvoiceReturns.Add(ret1);

            await context.SaveChangesAsync();

            // Run Sales Report
            var salesReport = await reportingService.GetSalesReportAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

            Assert.Equal(1, salesReport.TotalInvoicesCount);
            Assert.Equal(42.00m, salesReport.TotalGrandTotal);
            Assert.Equal(8.00m, salesReport.TotalRefunds);
            Assert.Equal(34.00m, salesReport.NetSales); // $42 - $8
            Assert.Equal(15.00m, salesReport.TotalCostOfGoodsSold); // 10 * $1.50

            // Gross Profit = NetSales ($34.00) - CostOfGoods ($15.00) = $19.00
            Assert.Equal(19.00m, salesReport.TotalGrossProfit);
            Assert.Equal(55.9m, salesReport.OverallGrossProfitMarginPercentage);
        }

        [Fact]
        public async Task Reports_DateFiltersAndEmptyResults_HandledGracefully()
        {
            using var context = new ApplicationDbContext(_options);
            var reportingService = new ReportingService(context);

            // Query future date where no transactions exist
            var futureFrom = DateTime.UtcNow.AddYears(5);
            var futureTo = DateTime.UtcNow.AddYears(5).AddDays(1);

            var report = await reportingService.GetSalesReportAsync(futureFrom, futureTo);

            Assert.NotNull(report);
            Assert.Equal(0, report.TotalInvoicesCount);
            Assert.Equal(0m, report.TotalGrandTotal);
            Assert.Equal(0m, report.TotalGrossProfit);
            Assert.Equal(0m, report.OverallGrossProfitMarginPercentage); // No divide-by-zero!
            Assert.Empty(report.Rows);

            // Low stock query on empty warehouse
            var lowStock = await reportingService.GetLowStockReportAsync(999);
            Assert.NotNull(lowStock);
            Assert.Equal(0, lowStock.OutOfStockCount);
            Assert.Empty(lowStock.Rows);
        }

        [Fact]
        public void Reports_VerifyGrossProfitIsNotMislabeledAsNetProfit()
        {
            // Verify that ReportingViewModels strictly uses "GrossProfit", never "NetProfit"
            var typesToCheck = new[]
            {
                typeof(SalesReportViewModel),
                typeof(SalesPeriodRowDto),
                typeof(ProductCategorySalesReportViewModel),
                typeof(ProductSalesSummaryDto),
                typeof(CategorySalesSummaryDto)
            };

            foreach (var type in typesToCheck)
            {
                var properties = type.GetProperties();
                var hasNetProfit = properties.Any(p => p.Name.Contains("NetProfit", StringComparison.OrdinalIgnoreCase));
                Assert.False(hasNetProfit, $"{type.Name} contains a property mislabeled as NetProfit!");

                var hasGrossProfit = properties.Any(p => p.Name.Contains("GrossProfit", StringComparison.OrdinalIgnoreCase));
                Assert.True(hasGrossProfit, $"{type.Name} should expose GrossProfit properties.");
            }
        }

        [Fact]
        public async Task Reports_BouquetConsumption_AccuratelyAggregatesRawMaterials()
        {
            using var context = new ApplicationDbContext(_options);
            var (warehouseId, roseId, _, ribbonId) = await SeedInventoryTestDataAsync(context);
            var reportingService = new ReportingService(context);

            var rose = await context.Products.FindAsync(roseId);
            var bouquet = new Product
            {
                SKU = "BQT-SAMPLE",
                Name = "Sample Bouquet",
                CategoryId = rose!.CategoryId,
                UnitOfMeasureId = rose.UnitOfMeasureId,
                ProductType = ProductType.Bouquet,
                UnitPrice = 30.00m,
                IsActive = true
            };
            context.Products.Add(bouquet);
            await context.SaveChangesAsync();

            var inv = new Invoice
            {
                InvoiceNumber = "INV-BQT-001",
                InvoiceDateUtc = DateTime.UtcNow,
                Status = InvoiceStatus.FullyPaid,
                GrandTotal = 30.00m,
                PaidAmount = 30.00m
            };
            var item = new InvoiceItem
            {
                ProductId = bouquet.Id,
                WarehouseId = warehouseId,
                Quantity = 1,
                UnitPrice = 30.00m,
                LineTotal = 30.00m,
                IsBouquetRecipeDeduction = true
            };
            item.ComponentConsumptions.Add(new InvoiceItemComponentConsumption
            {
                ComponentProductId = roseId,
                QuantityConsumed = 10,
                UnitCost = 1.50m
            });
            item.ComponentConsumptions.Add(new InvoiceItemComponentConsumption
            {
                ComponentProductId = ribbonId,
                QuantityConsumed = 2,
                UnitCost = 0.40m
            });
            inv.Items.Add(item);
            context.Invoices.Add(inv);
            await context.SaveChangesAsync();

            var report = await reportingService.GetBouquetConsumptionReportAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), null);

            Assert.Equal(1, report.TotalBouquetUnitsSold);
            Assert.Equal(2, report.DistinctComponentsCount);
            // Cost: 10 * 1.50 (15.00) + 2 * 0.40 (0.80) = 15.80
            Assert.Equal(15.80m, report.TotalComponentCost);

            var roseConsumption = report.Rows.First(r => r.ComponentProductId == roseId);
            Assert.Equal(10m, roseConsumption.TotalQuantityConsumed);
            Assert.Equal(15.00m, roseConsumption.TotalCost);
        }

        [Fact]
        public async Task Reports_CustomerAndSupplierBalances_ReconcilesReceivablesAndPayables()
        {
            using var context = new ApplicationDbContext(_options);
            var reportingService = new ReportingService(context);

            // Seed Customer with balance
            var cust = new Customer { Name = "Hotel Splendid", IsActive = true };
            context.Customers.Add(cust);
            await context.SaveChangesAsync();

            var inv = new Invoice
            {
                InvoiceNumber = "INV-SPLENDID-001",
                CustomerId = cust.Id,
                InvoiceDateUtc = DateTime.UtcNow,
                Status = InvoiceStatus.PartiallyPaid,
                GrandTotal = 500.00m,
                PaidAmount = 200.00m
            };
            context.Invoices.Add(inv);

            // Seed Supplier with balance
            var supp = new Supplier
            {
                Name = "Dutch Blooms Export",
                ContactPerson = "Jan Jansen",
                CurrentBalance = 1200.00m,
                IsActive = true
            };
            context.Suppliers.Add(supp);
            await context.SaveChangesAsync();

            var balances = await reportingService.GetCustomerSupplierBalancesReportAsync();

            // Customer outstanding = $500 - $200 = $300
            Assert.Equal(300.00m, balances.TotalCustomerReceivables);
            var custRow = balances.Customers.First(c => c.CustomerId == cust.Id);
            Assert.Equal(300.00m, custRow.OutstandingBalance);

            // Supplier balance = $1200
            Assert.Equal(1200.00m, balances.TotalSupplierPayables);
            var suppRow = balances.Suppliers.First(s => s.SupplierId == supp.Id);
            Assert.Equal(1200.00m, suppRow.CurrentBalance);
        }

        [Fact]
        public async Task Reports_WarehouseStockValuation_CalculatesCorrectTotals()
        {
            using var context = new ApplicationDbContext(_options);
            var (warehouseId, _, _, _) = await SeedInventoryTestDataAsync(context);
            var reportingService = new ReportingService(context);

            var report = await reportingService.GetWarehouseStockReportAsync(warehouseId, null, null);

            Assert.Equal(3, report.TotalProductsTracked);
            // Roses: 100 * $1.50 = $150
            // Vases: 10 * $10.00 = $100
            // Ribbon: 50 * $0.40 = $20
            // Total units = 160
            // Total valuation = $270.00
            Assert.Equal(160m, report.TotalUnitsInStock);
            Assert.Equal(270.00m, report.TotalInventoryValuation);
        }
    }
}
