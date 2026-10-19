using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Entities.Inventory;
using ward.Entities.Sales;
using Xunit;

namespace ward.Tests
{
    public class RelationalConstraintTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public RelationalConstraintTests()
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

        [Fact]
        public async Task Product_DuplicateSKU_ThrowsDbUpdateException()
        {
            using var context = new ApplicationDbContext(_options);

            var category = new Category { Name = "Fresh Cut Roses" };
            var uom = new UnitOfMeasure { Name = "Stem", Symbol = "stm" };
            context.Categories.Add(category);
            context.UnitsOfMeasure.Add(uom);
            await context.SaveChangesAsync();

            var product1 = new Product
            {
                SKU = "ROSE-RED",
                Name = "Red Rose",
                CategoryId = category.Id,
                UnitOfMeasureId = uom.Id,
                UnitPrice = 10m,
                CurrentAverageCost = 5m
            };
            context.Products.Add(product1);
            await context.SaveChangesAsync();

            var product2 = new Product
            {
                SKU = "ROSE-RED", // Duplicate SKU
                Name = "Red Rose Premium",
                CategoryId = category.Id,
                UnitOfMeasureId = uom.Id,
                UnitPrice = 12m,
                CurrentAverageCost = 6m
            };
            context.Products.Add(product2);

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await context.SaveChangesAsync();
            });
        }

        [Fact]
        public async Task Warehouse_DuplicateCode_ThrowsDbUpdateException()
        {
            using var context = new ApplicationDbContext(_options);

            var w1 = new Warehouse { Code = "MAIN", Name = "Main Floor" };
            context.Warehouses.Add(w1);
            await context.SaveChangesAsync();

            var w2 = new Warehouse { Code = "MAIN", Name = "Another Main" };
            context.Warehouses.Add(w2);

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await context.SaveChangesAsync();
            });
        }

        [Fact]
        public async Task StockItem_DuplicateProductAndWarehouse_ThrowsDbUpdateException()
        {
            using var context = new ApplicationDbContext(_options);

            var category = new Category { Name = "Roses" };
            var uom = new UnitOfMeasure { Name = "Stem", Symbol = "stm" };
            var warehouse = new Warehouse { Code = "MAIN", Name = "Main Floor" };
            context.Categories.Add(category);
            context.UnitsOfMeasure.Add(uom);
            context.Warehouses.Add(warehouse);
            await context.SaveChangesAsync();

            var product = new Product
            {
                SKU = "ROSE-PINK",
                Name = "Pink Rose",
                CategoryId = category.Id,
                UnitOfMeasureId = uom.Id,
                UnitPrice = 10m,
                CurrentAverageCost = 5m
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var stock1 = new StockItem
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                QuantityOnHand = 100m
            };
            context.StockItems.Add(stock1);
            await context.SaveChangesAsync();

            var stock2 = new StockItem
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id, // Duplicate (ProductId, WarehouseId)
                QuantityOnHand = 50m
            };
            context.StockItems.Add(stock2);

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await context.SaveChangesAsync();
            });
        }

        [Fact]
        public async Task Invoice_CascadeDeletesInvoiceItems_WhenInvoiceDeleted()
        {
            using var context = new ApplicationDbContext(_options);

            var category = new Category { Name = "Flowers" };
            var uom = new UnitOfMeasure { Name = "Stem", Symbol = "stm" };
            var warehouse = new Warehouse { Code = "MAIN", Name = "Main Floor" };
            context.Categories.Add(category);
            context.UnitsOfMeasure.Add(uom);
            context.Warehouses.Add(warehouse);
            await context.SaveChangesAsync();

            var product = new Product
            {
                SKU = "SUNFLOWER",
                Name = "Sunflower",
                CategoryId = category.Id,
                UnitOfMeasureId = uom.Id,
                UnitPrice = 25m,
                CurrentAverageCost = 12m
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var invoice = new Invoice
            {
                InvoiceNumber = "INV-DEL-TEST",
                InvoiceDateUtc = DateTime.UtcNow,
                SubTotal = 50m,
                GrandTotal = 50m,
                Status = InvoiceStatus.Draft,
                Items = new List<InvoiceItem>
                {
                    new InvoiceItem
                    {
                        ProductId = product.Id,
                        WarehouseId = warehouse.Id,
                        Quantity = 2m,
                        UnitPrice = 25m,
                        LineTotal = 50m
                    }
                }
            };
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            var invoiceId = invoice.Id;
            var invoiceItemId = invoice.Items.First().Id;

            // Delete invoice
            context.Invoices.Remove(invoice);
            await context.SaveChangesAsync();

            // Verify InvoiceItem was cascade deleted
            var itemExists = await context.InvoiceItems.AnyAsync(ii => ii.Id == invoiceItemId);
            Assert.False(itemExists);

            // Master product must still exist
            var productExists = await context.Products.AnyAsync(p => p.Id == product.Id);
            Assert.True(productExists);
        }
    }
}
