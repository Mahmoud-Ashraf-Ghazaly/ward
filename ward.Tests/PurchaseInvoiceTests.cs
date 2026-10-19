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
using ward.Entities.Purchasing;
using ward.Models.Purchasing;
using ward.Services.Purchasing;
using Xunit;

namespace ward.Tests
{
    public class PurchaseInvoiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public PurchaseInvoiceTests()
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

        private async Task<(int SupplierId, int WarehouseId, int RedRoseId, int WhiteRoseId, int RibbonId)> SeedPurchaseTestDataAsync(ApplicationDbContext context)
        {
            var supplier = new Supplier
            {
                Name = "Holland Flowers Direct",
                ContactPerson = "Pieter Van Dijk",
                Phone = "+31 20 123 4567",
                Email = "pieter@hollandflowers.test",
                CurrentBalance = 0,
                IsActive = true
            };
            context.Suppliers.Add(supplier);

            var warehouse = new Warehouse
            {
                Code = "MAIN-WH",
                Name = "Main Central Warehouse",
                IsDefault = true,
                IsActive = true
            };
            context.Warehouses.Add(warehouse);

            var catFlowers = new Category { Name = "Fresh Cut Flowers", IsActive = true };
            var catPackaging = new Category { Name = "Packaging Materials", IsActive = true };
            context.Categories.AddRange(catFlowers, catPackaging);

            var uomStem = new UnitOfMeasure { Name = "Stem", Symbol = "stm", AllowDecimals = false };
            var uomMeter = new UnitOfMeasure { Name = "Meter", Symbol = "m", AllowDecimals = true };
            context.UnitsOfMeasure.AddRange(uomStem, uomMeter);

            await context.SaveChangesAsync();

            var redRose = new Product
            {
                SKU = "FLW-RED-ROSE",
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
                SKU = "FLW-WHT-ROSE",
                Name = "White Avalanche Rose",
                CategoryId = catFlowers.Id,
                UnitOfMeasureId = uomStem.Id,
                UnitPrice = 4.50m,
                CurrentAverageCost = 2.50m,
                ProductType = ProductType.FreshFlower,
                IsActive = true
            };

            var ribbon = new Product
            {
                SKU = "PKG-SATIN-RIB",
                Name = "Pink Satin Ribbon",
                CategoryId = catPackaging.Id,
                UnitOfMeasureId = uomMeter.Id,
                UnitPrice = 3.00m,
                CurrentAverageCost = 1.00m,
                ProductType = ProductType.Accessory,
                IsActive = true
            };

            context.Products.AddRange(redRose, whiteRose, ribbon);
            await context.SaveChangesAsync();

            return (supplier.Id, warehouse.Id, redRose.Id, whiteRose.Id, ribbon.Id);
        }

        [Fact]
        public async Task CreatePurchaseInvoice_CalculatesTotalsAccuratelyOnServer()
        {
            using var context = new ApplicationDbContext(_options);
            var (supplierId, warehouseId, redRoseId, whiteRoseId, _) = await SeedPurchaseTestDataAsync(context);
            var service = new PurchaseService(context);

            // Invoice with:
            // 100 Red Roses @ $1.80 = $180.00
            // 50 White Roses @ $2.20 with $10 discount = (50 * 2.2) - 10 = $100.00
            // SubTotal = $280.00
            // Global Discount = $30.00 -> Taxable = $250.00
            // Tax Rate = 10% -> Tax Amount = $25.00
            // Grand Total = $275.00
            var model = new PurchaseInvoiceCreateViewModel
            {
                InvoiceNumber = "PINV-TEST-001",
                SupplierInvoiceNumber = "V-8849",
                SupplierId = supplierId,
                WarehouseId = warehouseId,
                InvoiceDate = DateTime.Today,
                DiscountAmount = 30.00m,
                TaxRatePercentage = 10.00m,
                PostImmediately = false, // Draft
                Items = new List<PurchaseInvoiceItemFormViewModel>
                {
                    new PurchaseInvoiceItemFormViewModel { ProductId = redRoseId, Quantity = 100, UnitCost = 1.80m, DiscountAmount = 0 },
                    new PurchaseInvoiceItemFormViewModel { ProductId = whiteRoseId, Quantity = 50, UnitCost = 2.20m, DiscountAmount = 10.00m }
                }
            };

            var (success, error, invoiceId) = await service.CreateInvoiceAsync(model, "user-test");

            Assert.True(success, error);
            Assert.True(invoiceId > 0);

            var invoice = await context.PurchaseInvoices
                .Include(p => p.Items)
                .FirstAsync(p => p.Id == invoiceId);

            Assert.Equal(PurchaseInvoiceStatus.Draft, invoice.Status);
            Assert.Equal(280.00m, invoice.SubTotal);
            Assert.Equal(30.00m, invoice.DiscountAmount);
            Assert.Equal(10.00m, invoice.TaxRatePercentage);
            Assert.Equal(25.00m, invoice.TaxAmount);
            Assert.Equal(275.00m, invoice.GrandTotal);

            // Stock must NOT be increased for Draft invoices!
            var redStock = await context.StockItems.FirstOrDefaultAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            Assert.Null(redStock);

            // Supplier balance must NOT be increased for Draft invoices!
            var supplier = await context.Suppliers.FindAsync(supplierId);
            Assert.Equal(0.00m, supplier!.CurrentBalance);
        }

        [Fact]
        public async Task PostPurchaseInvoice_TransactionallyIncreasesStock_AndUpdatesSupplierBalance_ExactlyOnce()
        {
            using var context = new ApplicationDbContext(_options);
            var (supplierId, warehouseId, redRoseId, whiteRoseId, _) = await SeedPurchaseTestDataAsync(context);
            var service = new PurchaseService(context);

            // Seed initial stock of 20 Red Roses @ $2.00 avg cost
            context.StockItems.Add(new StockItem
            {
                ProductId = redRoseId,
                WarehouseId = warehouseId,
                QuantityOnHand = 20,
                QuantityReserved = 0
            });
            await context.SaveChangesAsync();

            var token = Guid.NewGuid().ToString();
            var model = new PurchaseInvoiceCreateViewModel
            {
                InvoiceNumber = "PINV-POST-001",
                SupplierId = supplierId,
                WarehouseId = warehouseId,
                InvoiceDate = DateTime.Today,
                DiscountAmount = 0,
                TaxRatePercentage = 0,
                PostImmediately = false,
                IdempotencyToken = token,
                Items = new List<PurchaseInvoiceItemFormViewModel>
                {
                    // 80 Red Roses @ $1.50 -> New weighted avg: ((20 * 2.00) + (80 * 1.50)) / (20 + 80) = (40 + 120) / 100 = $1.60
                    new PurchaseInvoiceItemFormViewModel { ProductId = redRoseId, Quantity = 80, UnitCost = 1.50m },
                    // 40 White Roses @ $2.00
                    new PurchaseInvoiceItemFormViewModel { ProductId = whiteRoseId, Quantity = 40, UnitCost = 2.00m }
                }
            };

            var (createSuccess, _, invoiceId) = await service.CreateInvoiceAsync(model, "user-test");
            Assert.True(createSuccess);

            // Now post the invoice
            var (postSuccess, postError) = await service.PostInvoiceAsync(invoiceId, "user-test", token);
            Assert.True(postSuccess, postError);

            var invoice = await context.PurchaseInvoices.FindAsync(invoiceId);
            Assert.Equal(PurchaseInvoiceStatus.Posted, invoice!.Status);
            Assert.NotNull(invoice.PostedAtUtc);

            // 1. Verify Stock was increased
            var redStock = await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(100m, redStock.QuantityOnHand); // 20 initial + 80 received

            var whiteStock = await context.StockItems.FirstAsync(s => s.ProductId == whiteRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(40m, whiteStock.QuantityOnHand); // 0 + 40

            // 2. Verify Weighted Average Cost update
            var redProduct = await context.Products.FindAsync(redRoseId);
            Assert.Equal(1.60m, redProduct!.CurrentAverageCost);

            // 3. Verify Supplier Balance: Grand Total = (80 * 1.50) + (40 * 2.00) = 120 + 80 = $200.00
            var supplier = await context.Suppliers.FindAsync(supplierId);
            Assert.Equal(200.00m, supplier!.CurrentBalance);

            // 4. Verify StockMovement audit records
            var movements = await context.StockMovements
                .Where(m => m.ReferenceId == invoice.InvoiceNumber)
                .ToListAsync();
            Assert.Equal(2, movements.Count);
            Assert.All(movements, m => Assert.Equal(MovementType.PurchaseIn, m.MovementType));

            // 5. Duplicate Posting Protection: Posting already posted invoice rejects or returns idempotently without re-incrementing stock
            var (repostSuccess, repostError) = await service.PostInvoiceAsync(invoiceId, "user-test", token);
            Assert.True(repostSuccess); // Same idempotency token returns ok

            // Stock must STILL be 100 and 40 (NOT doubled!)
            var redStockAfter = await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(100m, redStockAfter.QuantityOnHand);

            // Re-posting without token fails
            var (failPost, failError) = await service.PostInvoiceAsync(invoiceId, "user-test", "different-token");
            Assert.False(failPost);
            Assert.Contains("Cannot post invoice in status 'Posted'", failError);
        }

        [Fact]
        public async Task PurchaseReturn_PartialAndFull_CorrectlyDecreasesStock_AndSupplierBalance()
        {
            using var context = new ApplicationDbContext(_options);
            var (supplierId, warehouseId, redRoseId, whiteRoseId, _) = await SeedPurchaseTestDataAsync(context);
            var service = new PurchaseService(context);

            // Create and post an invoice: 100 Red Roses @ $2.00 ($200) + 50 White Roses @ $3.00 ($150) = $350 GrandTotal
            var model = new PurchaseInvoiceCreateViewModel
            {
                InvoiceNumber = "PINV-RET-001",
                SupplierId = supplierId,
                WarehouseId = warehouseId,
                InvoiceDate = DateTime.Today,
                PostImmediately = true,
                Items = new List<PurchaseInvoiceItemFormViewModel>
                {
                    new PurchaseInvoiceItemFormViewModel { ProductId = redRoseId, Quantity = 100, UnitCost = 2.00m },
                    new PurchaseInvoiceItemFormViewModel { ProductId = whiteRoseId, Quantity = 50, UnitCost = 3.00m }
                }
            };
            var (invOk, _, invoiceId) = await service.CreateInvoiceAsync(model, "user-test");
            Assert.True(invOk);

            var supplier = await context.Suppliers.FindAsync(supplierId);
            Assert.Equal(350.00m, supplier!.CurrentBalance);

            // Prepare return model
            var returnPrep = await service.PrepareCreateReturnViewModelAsync(invoiceId);
            Assert.NotNull(returnPrep);
            Assert.Equal(2, returnPrep.Items.Count);

            // 1. Partial Return: Return 20 Red Roses ($40) due to wilting
            var redItem = returnPrep.Items.First(i => i.ProductId == redRoseId);
            redItem.ReturnQuantity = 20;
            redItem.Disposition = ReturnDisposition.Restocked;
            returnPrep.Reason = "Slightly bruised petals during transit";

            var (partOk, partErr, return1Id) = await service.CreateAndProcessReturnAsync(returnPrep, "user-test");
            Assert.True(partOk, partErr);
            Assert.True(return1Id > 0);

            // Verify invoice status updated to PartiallyReturned
            var invAfterPart = await context.PurchaseInvoices
                .Include(i => i.Items)
                .FirstAsync(i => i.Id == invoiceId);
            Assert.Equal(PurchaseInvoiceStatus.PartiallyReturned, invAfterPart.Status);

            var redInvItem = invAfterPart.Items.First(i => i.ProductId == redRoseId);
            Assert.Equal(20m, redInvItem.ReturnedQuantity);

            // Verify warehouse stock decreased: 100 - 20 = 80
            var redStock = await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(80m, redStock.QuantityOnHand);

            // Verify Supplier Balance credited: $350 - $40 = $310
            var suppAfterPart = await context.Suppliers.FindAsync(supplierId);
            Assert.Equal(310.00m, suppAfterPart!.CurrentBalance);

            // Verify StockMovement recorded with PurchaseReturnOut
            var ret1 = await context.PurchaseReturns.FindAsync(return1Id);
            var retMovement = await context.StockMovements.FirstAsync(m => m.ReferenceId == ret1!.ReturnNumber);
            Assert.Equal(MovementType.PurchaseReturnOut, retMovement.MovementType);
            Assert.Equal(-20m, retMovement.Quantity);

            // 2. Full Remaining Return:
            // Remaining Red Roses: 80
            // Remaining White Roses: 50
            var returnPrep2 = await service.PrepareCreateReturnViewModelAsync(invoiceId);
            Assert.NotNull(returnPrep2);

            var rItem2 = returnPrep2.Items.First(i => i.ProductId == redRoseId);
            Assert.Equal(80m, rItem2.EligibleQuantity);
            rItem2.ReturnQuantity = 80;

            var wItem2 = returnPrep2.Items.First(i => i.ProductId == whiteRoseId);
            Assert.Equal(50m, wItem2.EligibleQuantity);
            wItem2.ReturnQuantity = 50;
            wItem2.Disposition = ReturnDisposition.Discarded; // Damaged / spoiled goods

            returnPrep2.Reason = "Quality inspection failed remainder of shipment";

            var (fullOk, fullErr, return2Id) = await service.CreateAndProcessReturnAsync(returnPrep2, "user-test");
            Assert.True(fullOk, fullErr);

            // Verify invoice status transitioned to FullyReturned
            var invAfterFull = await context.PurchaseInvoices.FindAsync(invoiceId);
            Assert.Equal(PurchaseInvoiceStatus.FullyReturned, invAfterFull!.Status);

            // Verify stock is now 0 for both
            var redFinalStock = await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(0m, redFinalStock.QuantityOnHand);

            var whiteFinalStock = await context.StockItems.FirstAsync(s => s.ProductId == whiteRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(0m, whiteFinalStock.QuantityOnHand);

            // Verify Supplier Balance is now 0 (fully refunded)
            var suppFinal = await context.Suppliers.FindAsync(supplierId);
            Assert.Equal(0.00m, suppFinal!.CurrentBalance);

            // Verify Damaged goods used MovementType.DamagedDiscardOut
            var ret2 = await context.PurchaseReturns.FindAsync(return2Id);
            var whiteDiscardMovement = await context.StockMovements
                .FirstAsync(m => m.ReferenceId == ret2!.ReturnNumber && m.ProductId == whiteRoseId);
            Assert.Equal(MovementType.DamagedDiscardOut, whiteDiscardMovement.MovementType);
            Assert.Equal(-50m, whiteDiscardMovement.Quantity);
        }

        [Fact]
        public async Task PurchaseReturn_RejectsExcessReturns_AbovePurchasedQuantity()
        {
            using var context = new ApplicationDbContext(_options);
            var (supplierId, warehouseId, redRoseId, _, _) = await SeedPurchaseTestDataAsync(context);
            var service = new PurchaseService(context);

            // Invoice with 25 Red Roses
            var model = new PurchaseInvoiceCreateViewModel
            {
                InvoiceNumber = "PINV-EXCESS-001",
                SupplierId = supplierId,
                WarehouseId = warehouseId,
                PostImmediately = true,
                Items = new List<PurchaseInvoiceItemFormViewModel>
                {
                    new PurchaseInvoiceItemFormViewModel { ProductId = redRoseId, Quantity = 25, UnitCost = 2.00m }
                }
            };
            var (ok, _, invoiceId) = await service.CreateInvoiceAsync(model, "user-test");
            Assert.True(ok);

            var returnPrep = await service.PrepareCreateReturnViewModelAsync(invoiceId);
            Assert.NotNull(returnPrep);

            // Attempt to return 30 roses (only 25 purchased!)
            returnPrep.Items[0].ReturnQuantity = 30;
            returnPrep.Reason = "Too many ordered";

            var (retOk, retErr, _) = await service.CreateAndProcessReturnAsync(returnPrep, "user-test");

            Assert.False(retOk);
            Assert.Contains("exceeds eligible purchased quantity", retErr);

            // Verify stock remained untouched at 25
            var stock = await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(25m, stock.QuantityOnHand);
        }

        [Fact]
        public async Task PurchaseReturn_RejectsWhenWarehouseStockInsufficient()
        {
            using var context = new ApplicationDbContext(_options);
            var (supplierId, warehouseId, redRoseId, _, _) = await SeedPurchaseTestDataAsync(context);
            var service = new PurchaseService(context);

            // Invoice with 50 Red Roses
            var model = new PurchaseInvoiceCreateViewModel
            {
                InvoiceNumber = "PINV-NOSTOCK-001",
                SupplierId = supplierId,
                WarehouseId = warehouseId,
                PostImmediately = true,
                Items = new List<PurchaseInvoiceItemFormViewModel>
                {
                    new PurchaseInvoiceItemFormViewModel { ProductId = redRoseId, Quantity = 50, UnitCost = 2.00m }
                }
            };
            await service.CreateInvoiceAsync(model, "user-test");

            // Simulate stock was sold or transferred out, leaving only 10 on hand
            var stockItem = await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            stockItem.QuantityOnHand = 10;
            await context.SaveChangesAsync();

            var invoice = await context.PurchaseInvoices.FirstAsync(p => p.InvoiceNumber == "PINV-NOSTOCK-001");
            var returnPrep = await service.PrepareCreateReturnViewModelAsync(invoice.Id);
            Assert.NotNull(returnPrep);

            // Attempt to return 30 (eligible is 50, but warehouse only has 10)
            returnPrep.Items[0].ReturnQuantity = 30;
            returnPrep.Reason = "Defective shipment";

            var (retOk, retErr, _) = await service.CreateAndProcessReturnAsync(returnPrep, "user-test");

            Assert.False(retOk);
            Assert.Contains("Insufficient stock in warehouse", retErr);
        }

        [Fact]
        public async Task SupplierPayment_DecreasesSupplierBalance_AndTracksInvoicePaidAmount()
        {
            using var context = new ApplicationDbContext(_options);
            var (supplierId, warehouseId, redRoseId, _, _) = await SeedPurchaseTestDataAsync(context);
            var service = new PurchaseService(context);

            // Invoice for 100 Red Roses @ $2.50 = $250.00
            var model = new PurchaseInvoiceCreateViewModel
            {
                InvoiceNumber = "PINV-PAY-001",
                SupplierId = supplierId,
                WarehouseId = warehouseId,
                PostImmediately = true,
                Items = new List<PurchaseInvoiceItemFormViewModel>
                {
                    new PurchaseInvoiceItemFormViewModel { ProductId = redRoseId, Quantity = 100, UnitCost = 2.50m }
                }
            };
            var (ok, _, invoiceId) = await service.CreateInvoiceAsync(model, "user-test");
            Assert.True(ok);

            var supplier = await context.Suppliers.FindAsync(supplierId);
            Assert.Equal(250.00m, supplier!.CurrentBalance);

            // Record partial payment of $150.00 via Bank Transfer
            var paymentModel = new SupplierPaymentFormViewModel
            {
                SupplierId = supplierId,
                PurchaseInvoiceId = invoiceId,
                Amount = 150.00m,
                PaymentDate = DateTime.Today,
                PaymentMethod = PaymentMethod.BankTransfer,
                ReferenceNumber = "WIRE-77382"
            };

            var (payOk, payErr) = await service.RecordSupplierPaymentAsync(paymentModel, "user-test");
            Assert.True(payOk, payErr);

            // Verify Supplier Balance decreased: $250 - $150 = $100
            var suppAfter = await context.Suppliers.FindAsync(supplierId);
            Assert.Equal(100.00m, suppAfter!.CurrentBalance);

            // Verify Invoice Paid Amount and Balance
            var invAfter = await context.PurchaseInvoices.FindAsync(invoiceId);
            Assert.Equal(150.00m, invAfter!.PaidAmount);
            Assert.Equal(100.00m, invAfter.GrandTotal - invAfter.PaidAmount);

            // Record remaining payment of $100.00
            var paymentModel2 = new SupplierPaymentFormViewModel
            {
                SupplierId = supplierId,
                PurchaseInvoiceId = invoiceId,
                Amount = 100.00m,
                PaymentDate = DateTime.Today,
                PaymentMethod = PaymentMethod.Cash
            };
            var (payOk2, _) = await service.RecordSupplierPaymentAsync(paymentModel2, "user-test");
            Assert.True(payOk2);

            var suppFinal = await context.Suppliers.FindAsync(supplierId);
            Assert.Equal(0.00m, suppFinal!.CurrentBalance);

            var invFinal = await context.PurchaseInvoices.FindAsync(invoiceId);
            Assert.Equal(250.00m, invFinal!.PaidAmount);
        }

        [Fact]
        public async Task CannotIssueReturn_OnDraftOrCancelledInvoice()
        {
            using var context = new ApplicationDbContext(_options);
            var (supplierId, warehouseId, redRoseId, _, _) = await SeedPurchaseTestDataAsync(context);
            var service = new PurchaseService(context);

            var model = new PurchaseInvoiceCreateViewModel
            {
                InvoiceNumber = "PINV-DRAFT-001",
                SupplierId = supplierId,
                WarehouseId = warehouseId,
                PostImmediately = false, // Draft!
                Items = new List<PurchaseInvoiceItemFormViewModel>
                {
                    new PurchaseInvoiceItemFormViewModel { ProductId = redRoseId, Quantity = 50, UnitCost = 2.00m }
                }
            };
            var (ok, _, invoiceId) = await service.CreateInvoiceAsync(model, "user-test");
            Assert.True(ok);

            var prepReturn = await service.PrepareCreateReturnViewModelAsync(invoiceId);
            Assert.Null(prepReturn); // Must be null because invoice is not Posted!
        }
    }
}
