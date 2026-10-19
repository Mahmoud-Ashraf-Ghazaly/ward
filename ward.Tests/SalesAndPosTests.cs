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
using ward.Entities.Sales;
using ward.Models.Sales;
using ward.Services.Sales;
using Xunit;

namespace ward.Tests
{
    public class SalesAndPosTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public SalesAndPosTests()
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

        private async Task<(int CustomerId, int WarehouseId, int RedRoseId, int WrappingPaperId, int BouquetId)> SeedSalesTestDataAsync(ApplicationDbContext context)
        {
            var customer = new Customer
            {
                Name = "Alice Wonderland",
                Phone = "+1 555 123 4567",
                Email = "alice@example.com",
                Address = "100 Wonder Lane",
                IsActive = true
            };
            context.Customers.Add(customer);

            var warehouse = new Warehouse
            {
                Code = "WH-MAIN",
                Name = "Main Florist Hub",
                IsDefault = true,
                IsActive = true
            };
            context.Warehouses.Add(warehouse);

            var pieceUnit = new UnitOfMeasure { Name = "Piece", Symbol = "pcs", AllowDecimals = false };
            var meterUnit = new UnitOfMeasure { Name = "Meter", Symbol = "m", AllowDecimals = true };
            context.UnitsOfMeasure.AddRange(pieceUnit, meterUnit);

            var freshCat = new Category { Name = "Fresh Flowers", IsActive = true };
            var wrapCat = new Category { Name = "Wrapping", IsActive = true };
            var bouquetCat = new Category { Name = "Bouquets", IsActive = true };
            context.Categories.AddRange(freshCat, wrapCat, bouquetCat);
            await context.SaveChangesAsync();

            // 1. Red Rose (Raw flower)
            var redRose = new Product
            {
                SKU = "FLW-ROSE-RED",
                Barcode = "123456789012",
                Name = "Red Rose stem",
                CategoryId = freshCat.Id,
                UnitOfMeasureId = pieceUnit.Id,
                ProductType = ProductType.FreshFlower,
                UnitPrice = 3.50m,
                CurrentAverageCost = 1.50m,
                IsActive = true
            };
            context.Products.Add(redRose);

            // 2. Luxury Wrapping Paper
            var wrappingPaper = new Product
            {
                SKU = "MAT-WRAP-GOLD",
                Barcode = "123456789013",
                Name = "Gold Silk Wrapping Paper",
                CategoryId = wrapCat.Id,
                UnitOfMeasureId = meterUnit.Id,
                ProductType = ProductType.WrappingMaterial,
                UnitPrice = 2.00m,
                CurrentAverageCost = 0.80m,
                IsActive = true
            };
            context.Products.Add(wrappingPaper);
            await context.SaveChangesAsync();

            // 3. Romantic Rose Bouquet (Recipe-based bouquet)
            var bouquet = new Product
            {
                SKU = "BQT-ROSE-ROMANCE",
                Barcode = "123456789014",
                Name = "Romantic 10-Rose Bouquet",
                CategoryId = bouquetCat.Id,
                UnitOfMeasureId = pieceUnit.Id,
                ProductType = ProductType.Bouquet,
                UnitPrice = 49.99m,
                CurrentAverageCost = 0,
                IsActive = true
            };
            context.Products.Add(bouquet);
            await context.SaveChangesAsync();

            // Recipe: 10 Red Roses (0% waste), 2 Meters Wrapping Paper (0% waste)
            var recipe = new BouquetRecipe
            {
                BouquetProductId = bouquet.Id,
                Version = 1,
                LaborCost = 5.00m,
                PackagingCost = 2.00m,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            recipe.Items.Add(new RecipeItem
            {
                ComponentProductId = redRose.Id,
                QuantityRequired = 10,
                WasteMarginPercentage = 0
            });
            recipe.Items.Add(new RecipeItem
            {
                ComponentProductId = wrappingPaper.Id,
                QuantityRequired = 2,
                WasteMarginPercentage = 0
            });
            context.BouquetRecipes.Add(recipe);

            // Seed stock
            context.StockItems.AddRange(
                new StockItem { ProductId = redRose.Id, WarehouseId = warehouse.Id, QuantityOnHand = 100 },
                new StockItem { ProductId = wrappingPaper.Id, WarehouseId = warehouse.Id, QuantityOnHand = 50 },
                new StockItem { ProductId = bouquet.Id, WarehouseId = warehouse.Id, QuantityOnHand = 0 }
            );

            await context.SaveChangesAsync();

            return (customer.Id, warehouse.Id, redRose.Id, wrappingPaper.Id, bouquet.Id);
        }

        [Fact]
        public async Task PosCheckout_OrdinaryProduct_DeductsStockAndLogsLedger()
        {
            using var context = new ApplicationDbContext(_options);
            var (customerId, warehouseId, redRoseId, _, _) = await SeedSalesTestDataAsync(context);
            var salesService = new SalesService(context);

            var checkoutRequest = new PosCheckoutRequestDto
            {
                WarehouseId = warehouseId,
                CustomerId = customerId,
                PaymentMethod = PaymentMethod.Cash,
                AmountReceived = 50.00m,
                Items = new List<PosCheckoutItemRequestDto>
                {
                    new PosCheckoutItemRequestDto
                    {
                        ProductId = redRoseId,
                        Quantity = 10,
                        UnitPrice = 3.50m
                    }
                }
            };

            var result = await salesService.ProcessCheckoutAsync(checkoutRequest, "cashier1");

            Assert.True(result.Success);
            Assert.Equal(35.00m, result.GrandTotal);
            Assert.Equal(15.00m, result.ChangeGiven);

            // Verify stock deduction
            var stock = await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(90m, stock.QuantityOnHand); // 100 - 10

            // Verify stock movement ledger
            var movement = await context.StockMovements
                .FirstOrDefaultAsync(m => m.ProductId == redRoseId && m.MovementType == MovementType.SalesOut);
            Assert.NotNull(movement);
            Assert.Equal(-10m, movement.Quantity);
            Assert.Equal(result.InvoiceNumber, movement.ReferenceId);
        }

        [Fact]
        public async Task PosCheckout_RecipeBouquet_DeductsComponentsInsteadOfBouquetStock()
        {
            using var context = new ApplicationDbContext(_options);
            var (customerId, warehouseId, redRoseId, wrappingPaperId, bouquetId) = await SeedSalesTestDataAsync(context);
            var salesService = new SalesService(context);

            var checkoutRequest = new PosCheckoutRequestDto
            {
                WarehouseId = warehouseId,
                CustomerId = customerId,
                PaymentMethod = PaymentMethod.CreditCard,
                AmountReceived = 49.99m,
                Items = new List<PosCheckoutItemRequestDto>
                {
                    new PosCheckoutItemRequestDto
                    {
                        ProductId = bouquetId,
                        Quantity = 2, // 2 bouquets => 20 roses + 4 meters wrapping
                        UnitPrice = 49.99m
                    }
                }
            };

            var result = await salesService.ProcessCheckoutAsync(checkoutRequest, "cashier1");

            Assert.True(result.Success);

            // Bouquet stock itself should NOT change (remains 0)
            var bouquetStock = await context.StockItems.FirstAsync(s => s.ProductId == bouquetId && s.WarehouseId == warehouseId);
            Assert.Equal(0m, bouquetStock.QuantityOnHand);

            // Component stocks MUST be deducted
            var roseStock = await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            Assert.Equal(80m, roseStock.QuantityOnHand); // 100 - (10 * 2)

            var wrapStock = await context.StockItems.FirstAsync(s => s.ProductId == wrappingPaperId && s.WarehouseId == warehouseId);
            Assert.Equal(46m, wrapStock.QuantityOnHand); // 50 - (2 * 2)

            // Verify Component Consumptions recorded for invoice line item
            var invoiceItem = await context.InvoiceItems
                .Include(i => i.ComponentConsumptions)
                .FirstAsync(i => i.InvoiceId == result.InvoiceId);

            Assert.True(invoiceItem.IsBouquetRecipeDeduction);
            Assert.Equal(2, invoiceItem.ComponentConsumptions.Count);

            var roseConsumption = invoiceItem.ComponentConsumptions.First(c => c.ComponentProductId == redRoseId);
            Assert.Equal(20m, roseConsumption.QuantityConsumed);
        }

        [Fact]
        public async Task PosCheckout_RecipeBouquet_RejectsWhenComponentStockInsufficient()
        {
            using var context = new ApplicationDbContext(_options);
            var (customerId, warehouseId, redRoseId, _, bouquetId) = await SeedSalesTestDataAsync(context);
            var salesService = new SalesService(context);

            // Set red roses stock to only 5 (need 10 for 1 bouquet)
            var stockItem = await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId);
            stockItem.QuantityOnHand = 5;
            await context.SaveChangesAsync();

            var checkoutRequest = new PosCheckoutRequestDto
            {
                WarehouseId = warehouseId,
                CustomerId = customerId,
                PaymentMethod = PaymentMethod.Cash,
                AmountReceived = 49.99m,
                Items = new List<PosCheckoutItemRequestDto>
                {
                    new PosCheckoutItemRequestDto
                    {
                        ProductId = bouquetId,
                        Quantity = 1,
                        UnitPrice = 49.99m
                    }
                }
            };

            var result = await salesService.ProcessCheckoutAsync(checkoutRequest, "cashier1");

            Assert.False(result.Success);
            Assert.Contains("Insufficient stock for component", result.ErrorMessage);
        }

        [Fact]
        public async Task SalesReturn_FullReturn_RestoresOrdinaryProductStockAndUpdatesStatus()
        {
            using var context = new ApplicationDbContext(_options);
            var (customerId, warehouseId, redRoseId, _, _) = await SeedSalesTestDataAsync(context);
            var salesService = new SalesService(context);

            // 1. Sell 10 roses
            var checkout = await salesService.ProcessCheckoutAsync(new PosCheckoutRequestDto
            {
                WarehouseId = warehouseId,
                CustomerId = customerId,
                PaymentMethod = PaymentMethod.Cash,
                AmountReceived = 35.00m,
                Items = new List<PosCheckoutItemRequestDto>
                {
                    new PosCheckoutItemRequestDto { ProductId = redRoseId, Quantity = 10, UnitPrice = 3.50m }
                }
            }, "cashier1");

            Assert.True(checkout.Success);
            var stockAfterSale = (await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId)).QuantityOnHand;
            Assert.Equal(90m, stockAfterSale);

            // 2. Prepare return
            var returnModel = await salesService.PrepareCreateReturnViewModelAsync(checkout.InvoiceId);
            Assert.NotNull(returnModel);
            Assert.Single(returnModel.Items);
            Assert.Equal(10m, returnModel.Items[0].EligibleQuantity);

            // 3. Process full return of 10 roses (Restocked)
            returnModel.Reason = "Customer ordered too many";
            returnModel.RefundMethod = "Cash";
            returnModel.Items[0].ReturnQuantity = 10;
            returnModel.Items[0].Disposition = ReturnDisposition.Restocked;

            var (success, error, returnId) = await salesService.ProcessSalesReturnAsync(returnModel, "cashier1");

            Assert.True(success, error);
            Assert.True(returnId > 0);

            // Verify stock restored
            var stockAfterReturn = (await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId)).QuantityOnHand;
            Assert.Equal(100m, stockAfterReturn); // Fully restored to 100

            // Verify ReturnIn movement ledger
            var returnMovement = await context.StockMovements
                .FirstOrDefaultAsync(m => m.ProductId == redRoseId && m.MovementType == MovementType.ReturnIn);
            Assert.NotNull(returnMovement);
            Assert.Equal(10m, returnMovement.Quantity);

            // Verify Invoice status updated to FullyReturned
            var invoice = await context.Invoices.FindAsync(checkout.InvoiceId);
            Assert.Equal(InvoiceStatus.FullyReturned, invoice!.Status);
            Assert.Equal(0m, invoice.PaidAmount); // Paid amount reduced by refund
        }

        [Fact]
        public async Task SalesReturn_PartialReturn_UpdatesInvoiceStatusToPartiallyReturned()
        {
            using var context = new ApplicationDbContext(_options);
            var (customerId, warehouseId, redRoseId, _, _) = await SeedSalesTestDataAsync(context);
            var salesService = new SalesService(context);

            var checkout = await salesService.ProcessCheckoutAsync(new PosCheckoutRequestDto
            {
                WarehouseId = warehouseId,
                CustomerId = customerId,
                PaymentMethod = PaymentMethod.Cash,
                AmountReceived = 35.00m,
                Items = new List<PosCheckoutItemRequestDto>
                {
                    new PosCheckoutItemRequestDto { ProductId = redRoseId, Quantity = 10, UnitPrice = 3.50m }
                }
            }, "cashier1");

            var returnModel = await salesService.PrepareCreateReturnViewModelAsync(checkout.InvoiceId);
            returnModel!.Reason = "3 roses were extra";
            returnModel.Items[0].ReturnQuantity = 3; // Return 3 of 10
            returnModel.Items[0].Disposition = ReturnDisposition.Restocked;

            var (success, error, _) = await salesService.ProcessSalesReturnAsync(returnModel, "cashier1");

            Assert.True(success, error);

            var stockAfterReturn = (await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId)).QuantityOnHand;
            Assert.Equal(93m, stockAfterReturn); // 90 + 3

            var invoice = await context.Invoices.Include(i => i.Items).FirstAsync(i => i.Id == checkout.InvoiceId);
            Assert.Equal(InvoiceStatus.PartiallyReturned, invoice.Status);
            Assert.Equal(3m, invoice.Items.First().ReturnedQuantity);
            Assert.Equal(24.50m, invoice.PaidAmount); // $35 - (3 * $3.50)
        }

        [Fact]
        public async Task SalesReturn_ExcessiveReturnQuantity_IsRejected()
        {
            using var context = new ApplicationDbContext(_options);
            var (customerId, warehouseId, redRoseId, _, _) = await SeedSalesTestDataAsync(context);
            var salesService = new SalesService(context);

            var checkout = await salesService.ProcessCheckoutAsync(new PosCheckoutRequestDto
            {
                WarehouseId = warehouseId,
                CustomerId = customerId,
                PaymentMethod = PaymentMethod.Cash,
                AmountReceived = 35.00m,
                Items = new List<PosCheckoutItemRequestDto>
                {
                    new PosCheckoutItemRequestDto { ProductId = redRoseId, Quantity = 5, UnitPrice = 3.50m }
                }
            }, "cashier1");

            var returnModel = await salesService.PrepareCreateReturnViewModelAsync(checkout.InvoiceId);
            returnModel!.Reason = "Returning 6 roses when only 5 purchased";
            returnModel.Items[0].ReturnQuantity = 6; // Exceeds eligible 5

            var (success, error, _) = await salesService.ProcessSalesReturnAsync(returnModel, "cashier1");

            Assert.False(success);
            Assert.Contains("Remaining eligible quantity is 5", error);
        }

        [Fact]
        public async Task SalesReturn_RepeatedReturns_EnforcesRemainingEligibleQuantity()
        {
            using var context = new ApplicationDbContext(_options);
            var (customerId, warehouseId, redRoseId, _, _) = await SeedSalesTestDataAsync(context);
            var salesService = new SalesService(context);

            var checkout = await salesService.ProcessCheckoutAsync(new PosCheckoutRequestDto
            {
                WarehouseId = warehouseId,
                CustomerId = customerId,
                PaymentMethod = PaymentMethod.Cash,
                AmountReceived = 35.00m,
                Items = new List<PosCheckoutItemRequestDto>
                {
                    new PosCheckoutItemRequestDto { ProductId = redRoseId, Quantity = 10, UnitPrice = 3.50m }
                }
            }, "cashier1");

            // Return 4 items first
            var return1 = await salesService.PrepareCreateReturnViewModelAsync(checkout.InvoiceId);
            return1!.Reason = "First partial return";
            return1.Items[0].ReturnQuantity = 4;
            var (res1, _, _) = await salesService.ProcessSalesReturnAsync(return1, "cashier1");
            Assert.True(res1);

            // Second return: eligible should now be 6
            var return2 = await salesService.PrepareCreateReturnViewModelAsync(checkout.InvoiceId);
            Assert.Equal(6m, return2!.Items[0].EligibleQuantity);

            // Attempting to return 7 should fail
            return2.Reason = "Too many on second return";
            return2.Items[0].ReturnQuantity = 7;
            var (res2, err2, _) = await salesService.ProcessSalesReturnAsync(return2, "cashier1");
            Assert.False(res2);
            Assert.Contains("Remaining eligible quantity is 6", err2);

            // Returning exactly 6 should succeed and mark invoice FullyReturned
            return2.Items[0].ReturnQuantity = 6;
            return2.IdempotencyToken = Guid.NewGuid().ToString();
            var (res3, _, _) = await salesService.ProcessSalesReturnAsync(return2, "cashier1");
            Assert.True(res3);

            var invoice = await context.Invoices.FindAsync(checkout.InvoiceId);
            Assert.Equal(InvoiceStatus.FullyReturned, invoice!.Status);
        }

        [Fact]
        public async Task SalesReturn_RecipeBouquet_RestoresComponentQuantitiesToInventory()
        {
            using var context = new ApplicationDbContext(_options);
            var (customerId, warehouseId, redRoseId, wrappingPaperId, bouquetId) = await SeedSalesTestDataAsync(context);
            var salesService = new SalesService(context);

            // Sell 2 bouquets (deducts 20 roses + 4 meters wrapping)
            var checkout = await salesService.ProcessCheckoutAsync(new PosCheckoutRequestDto
            {
                WarehouseId = warehouseId,
                CustomerId = customerId,
                PaymentMethod = PaymentMethod.Cash,
                AmountReceived = 99.98m,
                Items = new List<PosCheckoutItemRequestDto>
                {
                    new PosCheckoutItemRequestDto { ProductId = bouquetId, Quantity = 2, UnitPrice = 49.99m }
                }
            }, "cashier1");

            Assert.True(checkout.Success);
            Assert.Equal(80m, (await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId)).QuantityOnHand);
            Assert.Equal(46m, (await context.StockItems.FirstAsync(s => s.ProductId == wrappingPaperId && s.WarehouseId == warehouseId)).QuantityOnHand);

            // Return 1 bouquet with Restocked disposition
            var returnModel = await salesService.PrepareCreateReturnViewModelAsync(checkout.InvoiceId);
            returnModel!.Reason = "Customer returned 1 bouquet intact";
            returnModel.RefundMethod = "CustomerCredit";
            returnModel.Items[0].ReturnQuantity = 1;
            returnModel.Items[0].Disposition = ReturnDisposition.Restocked;

            var (success, error, returnId) = await salesService.ProcessSalesReturnAsync(returnModel, "cashier1");

            Assert.True(success, error);

            // Returning 1 bouquet (out of 2) should restore half the consumed components:
            // 10 roses and 2 meters wrapping paper
            var roseStockAfter = (await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId)).QuantityOnHand;
            var wrapStockAfter = (await context.StockItems.FirstAsync(s => s.ProductId == wrappingPaperId && s.WarehouseId == warehouseId)).QuantityOnHand;

            Assert.Equal(90m, roseStockAfter); // 80 + 10
            Assert.Equal(48m, wrapStockAfter); // 46 + 2

            // Bouquet stock itself should still remain 0 (components were restored)
            var bouquetStock = (await context.StockItems.FirstAsync(s => s.ProductId == bouquetId && s.WarehouseId == warehouseId)).QuantityOnHand;
            Assert.Equal(0m, bouquetStock);

            // Verify return details view model displays restored components
            var details = await salesService.GetSalesReturnDetailsAsync(returnId);
            Assert.NotNull(details);
            Assert.Single(details.Items);
            Assert.Equal(2, details.Items[0].RestoredComponents.Count);
        }

        [Fact]
        public async Task SalesReturn_DamagedGoods_DoesNotRestoreStockToSellableInventory()
        {
            using var context = new ApplicationDbContext(_options);
            var (customerId, warehouseId, redRoseId, _, _) = await SeedSalesTestDataAsync(context);
            var salesService = new SalesService(context);

            var checkout = await salesService.ProcessCheckoutAsync(new PosCheckoutRequestDto
            {
                WarehouseId = warehouseId,
                CustomerId = customerId,
                PaymentMethod = PaymentMethod.Cash,
                AmountReceived = 35.00m,
                Items = new List<PosCheckoutItemRequestDto>
                {
                    new PosCheckoutItemRequestDto { ProductId = redRoseId, Quantity = 10, UnitPrice = 3.50m }
                }
            }, "cashier1");

            // Stock went from 100 to 90
            var stockBefore = (await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId)).QuantityOnHand;
            Assert.Equal(90m, stockBefore);

            // Return 5 roses as Damaged
            var returnModel = await salesService.PrepareCreateReturnViewModelAsync(checkout.InvoiceId);
            returnModel!.Reason = "Blooms arrived damaged in transit";
            returnModel.Items[0].ReturnQuantity = 5;
            returnModel.Items[0].Disposition = ReturnDisposition.Damaged;

            var (success, error, _) = await salesService.ProcessSalesReturnAsync(returnModel, "cashier1");

            Assert.True(success, error);

            // Stock should NOT increase because items were damaged/discarded
            var stockAfter = (await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId)).QuantityOnHand;
            Assert.Equal(90m, stockAfter);

            // No ReturnIn movement should be registered for damaged goods
            var returnMovements = await context.StockMovements
                .Where(m => m.ProductId == redRoseId && m.MovementType == MovementType.ReturnIn)
                .ToListAsync();
            Assert.Empty(returnMovements);
        }

        [Fact]
        public async Task SalesReturn_CustomerCredit_RecordsCreditPaymentConsistently()
        {
            using var context = new ApplicationDbContext(_options);
            var (customerId, warehouseId, redRoseId, _, _) = await SeedSalesTestDataAsync(context);
            var salesService = new SalesService(context);

            var checkout = await salesService.ProcessCheckoutAsync(new PosCheckoutRequestDto
            {
                WarehouseId = warehouseId,
                CustomerId = customerId,
                PaymentMethod = PaymentMethod.Cash,
                AmountReceived = 35.00m,
                Items = new List<PosCheckoutItemRequestDto>
                {
                    new PosCheckoutItemRequestDto { ProductId = redRoseId, Quantity = 10, UnitPrice = 3.50m }
                }
            }, "cashier1");

            var returnModel = await salesService.PrepareCreateReturnViewModelAsync(checkout.InvoiceId);
            returnModel!.Reason = "Exchange requested - store credit";
            returnModel.RefundMethod = "CustomerCredit";
            returnModel.Items[0].ReturnQuantity = 4;
            returnModel.Items[0].Disposition = ReturnDisposition.Restocked;

            var (success, error, returnId) = await salesService.ProcessSalesReturnAsync(returnModel, "cashier1");

            Assert.True(success, error);

            var returnRecord = await context.InvoiceReturns.FindAsync(returnId);
            Assert.Equal("CustomerCredit", returnRecord!.RefundMethod);
            Assert.Equal(14.00m, returnRecord.TotalRefundAmount); // 4 * $3.50

            // Verify negative refund payment logged
            var refundPayment = await context.Payments
                .FirstOrDefaultAsync(p => p.InvoiceId == checkout.InvoiceId && p.Amount < 0);
            Assert.NotNull(refundPayment);
            Assert.Equal(-14.00m, refundPayment.Amount);
            Assert.Equal(PaymentMethod.GiftCard, refundPayment.PaymentMethod); // Store credit mapped
        }

        [Fact]
        public async Task SalesReturn_IdempotencyToken_PreventsDuplicateReturnProcessing()
        {
            using var context = new ApplicationDbContext(_options);
            var (customerId, warehouseId, redRoseId, _, _) = await SeedSalesTestDataAsync(context);
            var salesService = new SalesService(context);

            var checkout = await salesService.ProcessCheckoutAsync(new PosCheckoutRequestDto
            {
                WarehouseId = warehouseId,
                CustomerId = customerId,
                PaymentMethod = PaymentMethod.Cash,
                AmountReceived = 35.00m,
                Items = new List<PosCheckoutItemRequestDto>
                {
                    new PosCheckoutItemRequestDto { ProductId = redRoseId, Quantity = 10, UnitPrice = 3.50m }
                }
            }, "cashier1");

            var idempotencyToken = "IDEMP-RET-TEST-001";
            var returnModel = await salesService.PrepareCreateReturnViewModelAsync(checkout.InvoiceId);
            returnModel!.Reason = "Initial return submission";
            returnModel.IdempotencyToken = idempotencyToken;
            returnModel.Items[0].ReturnQuantity = 5;
            returnModel.Items[0].Disposition = ReturnDisposition.Restocked;

            // First submission
            var (success1, _, returnId1) = await salesService.ProcessSalesReturnAsync(returnModel, "cashier1");
            Assert.True(success1);

            var stockAfterFirst = (await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId)).QuantityOnHand;
            Assert.Equal(95m, stockAfterFirst); // 90 + 5

            // Resubmit exact same return request with same idempotency token
            var (success2, _, returnId2) = await salesService.ProcessSalesReturnAsync(returnModel, "cashier1");
            Assert.True(success2);
            Assert.Equal(returnId1, returnId2); // Returns existing return ID

            // Stock should NOT be restored a second time!
            var stockAfterSecond = (await context.StockItems.FirstAsync(s => s.ProductId == redRoseId && s.WarehouseId == warehouseId)).QuantityOnHand;
            Assert.Equal(95m, stockAfterSecond);

            // Total returns count in DB remains 1
            var returnsCount = await context.InvoiceReturns.CountAsync(r => r.InvoiceId == checkout.InvoiceId);
            Assert.Equal(1, returnsCount);
        }
    }
}
