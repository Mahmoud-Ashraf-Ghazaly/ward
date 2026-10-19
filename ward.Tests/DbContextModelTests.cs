using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Entities.Inventory;
using ward.Entities.Production;
using ward.Entities.Sales;
using Xunit;

namespace ward.Tests
{
    public class DbContextModelTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public void DbContext_CanBeConstructed_AndModelIsConfigured()
        {
            using var context = CreateInMemoryDbContext();
            Assert.NotNull(context.Model);
        }

        [Fact]
        public void Product_HasExpectedDecimalPrecisionAndConcurrencyToken()
        {
            using var context = CreateInMemoryDbContext();
            var productType = context.Model.FindEntityType(typeof(Product));
            Assert.NotNull(productType);

            // RowVersion concurrency token
            var rowVersionProp = productType.FindProperty(nameof(Product.RowVersion));
            Assert.NotNull(rowVersionProp);
            Assert.True(rowVersionProp.IsConcurrencyToken);

            // UnitPrice precision: 18, 2
            var unitPriceProp = productType.FindProperty(nameof(Product.UnitPrice));
            Assert.NotNull(unitPriceProp);
            Assert.Equal(18, unitPriceProp.GetPrecision());
            Assert.Equal(2, unitPriceProp.GetScale());

            // CurrentAverageCost precision: 18, 2
            var costProp = productType.FindProperty(nameof(Product.CurrentAverageCost));
            Assert.NotNull(costProp);
            Assert.Equal(18, costProp.GetPrecision());
            Assert.Equal(2, costProp.GetScale());

            // MinimumStockLevel precision: 18, 4
            var minStockProp = productType.FindProperty(nameof(Product.MinimumStockLevel));
            Assert.NotNull(minStockProp);
            Assert.Equal(18, minStockProp.GetPrecision());
            Assert.Equal(4, minStockProp.GetScale());
        }

        [Fact]
        public void StockItem_HasExpectedQuantityPrecisionAndConcurrencyToken()
        {
            using var context = CreateInMemoryDbContext();
            var stockItemType = context.Model.FindEntityType(typeof(StockItem));
            Assert.NotNull(stockItemType);

            // Concurrency token
            var rowVersion = stockItemType.FindProperty(nameof(StockItem.RowVersion));
            Assert.NotNull(rowVersion);
            Assert.True(rowVersion.IsConcurrencyToken);

            // QuantityOnHand precision: 18, 4
            var qtyOnHand = stockItemType.FindProperty(nameof(StockItem.QuantityOnHand));
            Assert.NotNull(qtyOnHand);
            Assert.Equal(18, qtyOnHand.GetPrecision());
            Assert.Equal(4, qtyOnHand.GetScale());

            // QuantityReserved precision: 18, 4
            var qtyReserved = stockItemType.FindProperty(nameof(StockItem.QuantityReserved));
            Assert.NotNull(qtyReserved);
            Assert.Equal(18, qtyReserved.GetPrecision());
            Assert.Equal(4, qtyReserved.GetScale());
        }

        [Fact]
        public void StockMovement_HasQuantityAndCostPrecision()
        {
            using var context = CreateInMemoryDbContext();
            var movementType = context.Model.FindEntityType(typeof(StockMovement));
            Assert.NotNull(movementType);

            var qty = movementType.FindProperty(nameof(StockMovement.Quantity));
            Assert.NotNull(qty);
            Assert.Equal(18, qty.GetPrecision());
            Assert.Equal(4, qty.GetScale());

            var unitCost = movementType.FindProperty(nameof(StockMovement.UnitCost));
            Assert.NotNull(unitCost);
            Assert.Equal(18, unitCost.GetPrecision());
            Assert.Equal(2, unitCost.GetScale());
        }

        [Fact]
        public void Invoice_HasGrandTotalAndTaxPrecision()
        {
            using var context = CreateInMemoryDbContext();
            var invoiceType = context.Model.FindEntityType(typeof(Invoice));
            Assert.NotNull(invoiceType);

            var grandTotal = invoiceType.FindProperty(nameof(Invoice.GrandTotal));
            Assert.NotNull(grandTotal);
            Assert.Equal(18, grandTotal.GetPrecision());
            Assert.Equal(2, grandTotal.GetScale());

            var taxRate = invoiceType.FindProperty(nameof(Invoice.TaxRatePercentage));
            Assert.NotNull(taxRate);
            Assert.Equal(5, taxRate.GetPrecision());
            Assert.Equal(2, taxRate.GetScale());
        }

        [Fact]
        public async Task Product_CanBePersisted_AndRetrievedWithNavigationProperties()
        {
            using var context = CreateInMemoryDbContext();

            var category = new Category { Name = "Fresh Cut Flowers" };
            var uom = new UnitOfMeasure { Name = "Stem", Symbol = "stm" };
            context.Categories.Add(category);
            context.UnitsOfMeasure.Add(uom);
            await context.SaveChangesAsync();

            var product = new Product
            {
                SKU = "ROSE-RED-01",
                Name = "Red Naomi Rose",
                CategoryId = category.Id,
                UnitOfMeasureId = uom.Id,
                UnitPrice = 15.00m,
                CurrentAverageCost = 7.50m,
                MinimumStockLevel = 50.0000m,
                ProductType = ProductType.FreshFlower
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var retrieved = await context.Products
                .Include(p => p.Category)
                .Include(p => p.UnitOfMeasure)
                .FirstOrDefaultAsync(p => p.SKU == "ROSE-RED-01");

            Assert.NotNull(retrieved);
            Assert.Equal("Red Naomi Rose", retrieved.Name);
            Assert.Equal("Fresh Cut Flowers", retrieved.Category.Name);
            Assert.Equal("Stem", retrieved.UnitOfMeasure.Name);
            Assert.Equal(15.00m, retrieved.UnitPrice);
        }

        [Fact]
        public async Task BouquetRecipe_CanBeLinkedToProductsAndExploded()
        {
            using var context = CreateInMemoryDbContext();

            var category = new Category { Name = "Arrangements" };
            var uomStem = new UnitOfMeasure { Name = "Stem", Symbol = "stm" };
            var uomBouquet = new UnitOfMeasure { Name = "Bouquet", Symbol = "bq" };
            context.Categories.Add(category);
            context.UnitsOfMeasure.AddRange(uomStem, uomBouquet);
            await context.SaveChangesAsync();

            var rose = new Product
            {
                SKU = "ROSE-01",
                Name = "Red Rose",
                CategoryId = category.Id,
                UnitOfMeasureId = uomStem.Id,
                UnitPrice = 10.00m,
                CurrentAverageCost = 5.00m,
                ProductType = ProductType.FreshFlower
            };

            var lily = new Product
            {
                SKU = "LILY-01",
                Name = "White Lily",
                CategoryId = category.Id,
                UnitOfMeasureId = uomStem.Id,
                UnitPrice = 20.00m,
                CurrentAverageCost = 10.00m,
                ProductType = ProductType.FreshFlower
            };

            var bouquet = new Product
            {
                SKU = "BQ-ROMANCE",
                Name = "Romance Bouquet",
                CategoryId = category.Id,
                UnitOfMeasureId = uomBouquet.Id,
                UnitPrice = 150.00m,
                CurrentAverageCost = 0.00m,
                ProductType = ProductType.Bouquet
            };

            context.Products.AddRange(rose, lily, bouquet);
            await context.SaveChangesAsync();

            var recipe = new BouquetRecipe
            {
                BouquetProductId = bouquet.Id,
                LaborCost = 25.00m,
                PackagingCost = 15.00m,
                Instructions = "Hand-tied with silk ribbon",
                Items = new List<RecipeItem>
                {
                    new RecipeItem { ComponentProductId = rose.Id, QuantityRequired = 10.0000m, WasteMarginPercentage = 5.00m },
                    new RecipeItem { ComponentProductId = lily.Id, QuantityRequired = 3.0000m, WasteMarginPercentage = 0.00m }
                }
            };

            context.BouquetRecipes.Add(recipe);
            await context.SaveChangesAsync();

            var loadedRecipe = await context.BouquetRecipes
                .Include(r => r.BouquetProduct)
                .Include(r => r.Items)
                .ThenInclude(i => i.ComponentProduct)
                .FirstOrDefaultAsync(r => r.BouquetProductId == bouquet.Id);

            Assert.NotNull(loadedRecipe);
            Assert.Equal("Romance Bouquet", loadedRecipe.BouquetProduct.Name);
            Assert.Equal(2, loadedRecipe.Items.Count);

            // Verify cost calculation invariant: (10 * 5.00 * 1.05) + (3 * 10.00) + 25 (labor) + 15 (packaging)
            decimal estimatedCost = 0;
            foreach (var item in loadedRecipe.Items)
            {
                var componentCost = item.ComponentProduct.CurrentAverageCost;
                var effectiveQty = item.QuantityRequired * (1 + (item.WasteMarginPercentage / 100m));
                estimatedCost += effectiveQty * componentCost;
            }
            estimatedCost += loadedRecipe.LaborCost + loadedRecipe.PackagingCost;

            // (10 * 5 * 1.05 = 52.50) + (3 * 10 = 30) + 25 + 15 = 122.50
            Assert.Equal(122.50m, estimatedCost);
        }

        [Fact]
        public async Task Invoice_LifecycleAndPaymentTracking_CalculatesBalanceAccurately()
        {
            using var context = CreateInMemoryDbContext();

            var customer = new Customer { Name = "Jane Doe", Phone = "01000000000" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var invoice = new Invoice
            {
                InvoiceNumber = "INV-2026-0001",
                CustomerId = customer.Id,
                InvoiceDateUtc = DateTime.UtcNow,
                SubTotal = 200.00m,
                TaxRatePercentage = 14.00m,
                TaxAmount = 28.00m,
                DiscountAmount = 10.00m,
                GrandTotal = 218.00m, // 200 + 28 - 10
                PaidAmount = 0.00m,
                Status = InvoiceStatus.Confirmed
            };

            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            // Record first payment
            var payment1 = new Payment
            {
                InvoiceId = invoice.Id,
                Amount = 100.00m,
                PaymentMethod = PaymentMethod.Cash
            };
            invoice.PaidAmount += payment1.Amount;
            invoice.Status = invoice.PaidAmount < invoice.GrandTotal ? InvoiceStatus.PartiallyPaid : InvoiceStatus.FullyPaid;
            context.Payments.Add(payment1);
            await context.SaveChangesAsync();

            Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
            Assert.Equal(118.00m, invoice.GrandTotal - invoice.PaidAmount);

            // Record remaining payment
            var payment2 = new Payment
            {
                InvoiceId = invoice.Id,
                Amount = 118.00m,
                PaymentMethod = PaymentMethod.CreditCard
            };
            invoice.PaidAmount += payment2.Amount;
            invoice.Status = invoice.PaidAmount >= invoice.GrandTotal ? InvoiceStatus.FullyPaid : InvoiceStatus.PartiallyPaid;
            context.Payments.Add(payment2);
            await context.SaveChangesAsync();

            Assert.Equal(InvoiceStatus.FullyPaid, invoice.Status);
            Assert.Equal(218.00m, invoice.PaidAmount);
        }
    }
}
