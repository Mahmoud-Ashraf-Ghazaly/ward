using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ward.Constants;
using ward.Controllers;
using ward.Data;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Models.Catalog;
using ward.Services.Barcode;
using ward.Services.Catalog;
using Xunit;

namespace ward.Tests
{
    public class ProductCatalogTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private async Task<(int CategoryId, int StemUomId, int MeterUomId)> SeedBaseLookupDataAsync(ApplicationDbContext context)
        {
            var category = new Category { Name = "Fresh Flowers", IsActive = true };
            var stemUom = new UnitOfMeasure { Name = "Stem", Symbol = "stm", AllowDecimals = false };
            var meterUom = new UnitOfMeasure { Name = "Meter", Symbol = "m", AllowDecimals = true };

            context.Categories.Add(category);
            context.UnitsOfMeasure.AddRange(stemUom, meterUom);
            await context.SaveChangesAsync();

            return (category.Id, stemUom.Id, meterUom.Id);
        }

        // ==================== 1. Barcode Service Tests ====================

        [Fact]
        public void BarcodeService_GeneratesUniqueBarcode_AndValidSvg()
        {
            var barcodeService = new BarcodeService();

            var barcode1 = barcodeService.GenerateUniqueBarcode("FLW");
            var barcode2 = barcodeService.GenerateUniqueBarcode("FLW");

            Assert.StartsWith("FLW-", barcode1);
            Assert.NotEqual(barcode1, barcode2);
            Assert.True(barcodeService.IsValidBarcode(barcode1));

            var svg = barcodeService.GenerateCode128Svg(barcode1);
            Assert.Contains("<svg", svg);
            Assert.Contains("<rect", svg);
            Assert.Contains(barcode1, svg);
        }

        // ==================== 2. Category Service Tests ====================

        [Fact]
        public async Task CategoryService_Create_DuplicateRejection_AndProductDeleteGuard()
        {
            using var context = CreateInMemoryDbContext();
            var service = new CategoryService(context);

            // 1. Create Category
            var result = await service.CreateAsync(new CategoryFormViewModel
            {
                Name = "Tropical Orchids",
                Description = "Exotic cut and potted orchids",
                IsActive = true
            });
            Assert.True(result.Success);

            // 2. Reject Duplicate Category Name (case-insensitive)
            var dupResult = await service.CreateAsync(new CategoryFormViewModel
            {
                Name = "tropical orchids"
            });
            Assert.False(dupResult.Success);
            Assert.Contains("already exists", dupResult.Error);

            // 3. Assign a product to category and verify delete is guarded
            var uom = new UnitOfMeasure { Name = "Piece", Symbol = "pc" };
            context.UnitsOfMeasure.Add(uom);
            await context.SaveChangesAsync();

            var createdCategory = await context.Categories.FirstAsync(c => c.Name == "Tropical Orchids");
            context.Products.Add(new Product
            {
                SKU = "ORCHID-01",
                Name = "White Phalaenopsis",
                CategoryId = createdCategory.Id,
                UnitOfMeasureId = uom.Id,
                UnitPrice = 45m,
                CurrentAverageCost = 20m
            });
            await context.SaveChangesAsync();

            // Try to delete category with assigned product
            var deleteResult = await service.DeleteAsync(createdCategory.Id);
            Assert.False(deleteResult.Success);
            Assert.Contains("Cannot delete category", deleteResult.Error);
        }

        // ==================== 3. Product Service CRUD & Validation Tests ====================

        [Theory]
        [InlineData(ProductType.FreshFlower)]
        [InlineData(ProductType.ArtificialFlower)]
        [InlineData(ProductType.Bouquet)]
        [InlineData(ProductType.Vase)]
        [InlineData(ProductType.WrappingMaterial)]
        [InlineData(ProductType.Accessory)]
        [InlineData(ProductType.Service)]
        public async Task ProductService_SupportsAllDomainProductTypes(ProductType productType)
        {
            using var context = CreateInMemoryDbContext();
            var barcodeService = new BarcodeService();
            var productService = new ProductService(context, barcodeService);

            var (catId, stemUomId, _) = await SeedBaseLookupDataAsync(context);

            var model = new ProductCreateEditViewModel
            {
                SKU = $"SKU-{productType}",
                Name = $"Item of type {productType}",
                ProductType = productType,
                CategoryId = catId,
                UnitOfMeasureId = stemUomId,
                UnitPrice = 25.50m,
                CurrentAverageCost = 10.00m,
                MinimumStockLevel = 5.0000m,
                IsActive = true
            };

            var (success, error, productId) = await productService.CreateAsync(model);
            Assert.True(success, error);
            Assert.True(productId > 0);

            var saved = await productService.GetDetailsByIdAsync(productId);
            Assert.NotNull(saved);
            Assert.Equal(productType, saved.ProductType);
            Assert.Equal(25.50m, saved.UnitPrice);
            Assert.Equal(10.00m, saved.CurrentAverageCost);
        }

        [Fact]
        public async Task ProductService_RejectsDuplicateSKU_AndDuplicateBarcode()
        {
            using var context = CreateInMemoryDbContext();
            var barcodeService = new BarcodeService();
            var productService = new ProductService(context, barcodeService);

            var (catId, stemUomId, _) = await SeedBaseLookupDataAsync(context);

            var p1 = new ProductCreateEditViewModel
            {
                SKU = "ROSE-DUTCH-RED",
                Barcode = "BAR-10001",
                Name = "Dutch Red Rose",
                CategoryId = catId,
                UnitOfMeasureId = stemUomId,
                UnitPrice = 15m,
                CurrentAverageCost = 7m
            };
            var (success1, _, _) = await productService.CreateAsync(p1);
            Assert.True(success1);

            // 1. Attempt Duplicate SKU
            var pDuplicateSku = new ProductCreateEditViewModel
            {
                SKU = "rose-dutch-red", // Case-insensitive duplicate
                Barcode = "BAR-10002",
                Name = "Another Red Rose",
                CategoryId = catId,
                UnitOfMeasureId = stemUomId,
                UnitPrice = 16m,
                CurrentAverageCost = 8m
            };
            var (successDupSku, errorDupSku, _) = await productService.CreateAsync(pDuplicateSku);
            Assert.False(successDupSku);
            Assert.Contains("SKU 'ROSE-DUTCH-RED' is already assigned", errorDupSku);

            // 2. Attempt Duplicate Barcode
            var pDuplicateBarcode = new ProductCreateEditViewModel
            {
                SKU = "ROSE-DUTCH-WHITE",
                Barcode = "BAR-10001", // Duplicate barcode
                Name = "Dutch White Rose",
                CategoryId = catId,
                UnitOfMeasureId = stemUomId,
                UnitPrice = 15m,
                CurrentAverageCost = 7m
            };
            var (successDupBar, errorDupBar, _) = await productService.CreateAsync(pDuplicateBarcode);
            Assert.False(successDupBar);
            Assert.Contains("Barcode 'BAR-10001' is already assigned", errorDupBar);
        }

        [Fact]
        public async Task ProductService_AutoGeneratesBarcode_WhenLeftEmpty()
        {
            using var context = CreateInMemoryDbContext();
            var barcodeService = new BarcodeService();
            var productService = new ProductService(context, barcodeService);

            var (catId, stemUomId, _) = await SeedBaseLookupDataAsync(context);

            var model = new ProductCreateEditViewModel
            {
                SKU = "ROSE-YELLOW",
                Barcode = null, // Blank barcode
                Name = "Yellow Rose",
                CategoryId = catId,
                UnitOfMeasureId = stemUomId,
                UnitPrice = 12m,
                CurrentAverageCost = 6m
            };

            var (success, _, productId) = await productService.CreateAsync(model);
            Assert.True(success);

            var details = await productService.GetDetailsByIdAsync(productId);
            Assert.NotNull(details);
            Assert.False(string.IsNullOrWhiteSpace(details.Barcode));
            Assert.StartsWith("FLW-", details.Barcode);
        }

        [Fact]
        public async Task ProductService_SupportsFractionalQuantities_AndDecimalPrices()
        {
            using var context = CreateInMemoryDbContext();
            var barcodeService = new BarcodeService();
            var productService = new ProductService(context, barcodeService);

            var (catId, _, meterUomId) = await SeedBaseLookupDataAsync(context);

            // Floral Satin Ribbon measured in fractional meters
            var ribbon = new ProductCreateEditViewModel
            {
                SKU = "RIBBON-SILK-GOLD",
                Name = "Gold Silk Ribbon",
                ProductType = ProductType.WrappingMaterial,
                CategoryId = catId,
                UnitOfMeasureId = meterUomId,
                UnitPrice = 3.75m,
                CurrentAverageCost = 1.25m,
                MinimumStockLevel = 12.5000m // Fractional 12.5 meters
            };

            var (success, _, productId) = await productService.CreateAsync(ribbon);
            Assert.True(success);

            var retrieved = await productService.GetDetailsByIdAsync(productId);
            Assert.NotNull(retrieved);
            Assert.Equal(12.5000m, retrieved.MinimumStockLevel);
            Assert.Equal(3.75m, retrieved.UnitPrice);
            Assert.Equal(1.25m, retrieved.CurrentAverageCost);
            Assert.True(retrieved.AllowDecimals);
        }

        [Fact]
        public async Task ProductService_LookupBySkuOrBarcode_FindsProduct()
        {
            using var context = CreateInMemoryDbContext();
            var barcodeService = new BarcodeService();
            var productService = new ProductService(context, barcodeService);

            var (catId, stemUomId, _) = await SeedBaseLookupDataAsync(context);

            var product = new ProductCreateEditViewModel
            {
                SKU = "HYDRANGEA-BLUE",
                Barcode = "8901234567890",
                Name = "Blue Dutch Hydrangea",
                ProductType = ProductType.FreshFlower,
                CategoryId = catId,
                UnitOfMeasureId = stemUomId,
                UnitPrice = 35.00m,
                CurrentAverageCost = 18.00m,
                IsActive = true
            };
            await productService.CreateAsync(product);

            // 1. Lookup by SKU (case insensitive)
            var bySku = await productService.LookupBySkuOrBarcodeAsync("hydrangea-blue");
            Assert.NotNull(bySku);
            Assert.Equal("Blue Dutch Hydrangea", bySku.Name);
            Assert.Equal(35.00m, bySku.UnitPrice);

            // 2. Lookup by Barcode
            var byBarcode = await productService.LookupBySkuOrBarcodeAsync(" 8901234567890 ");
            Assert.NotNull(byBarcode);
            Assert.Equal("HYDRANGEA-BLUE", byBarcode.SKU);

            // 3. Lookup non-existent
            var nonExistent = await productService.LookupBySkuOrBarcodeAsync("UNKNOWN-CODE");
            Assert.Null(nonExistent);
        }

        // ==================== 4. Authorization & Controller Security Tests ====================

        [Fact]
        public void Controllers_EnforceManageCatalogPolicy_OnSensitiveEndpoints()
        {
            // Verify CategoriesController has ManageCatalog policy
            var categoryAttr = typeof(CategoriesController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(categoryAttr);
            Assert.Equal(AppPolicies.ManageCatalog, categoryAttr.Policy);

            // Verify ProductsController has ManageCatalog policy
            var productAttr = typeof(ProductsController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(productAttr);
            Assert.Equal(AppPolicies.ManageCatalog, productAttr.Policy);

            // Verify Create, Edit, ToggleActive in ProductsController require authorization (not AllowAnonymous)
            var createMethod = typeof(ProductsController).GetMethod(nameof(ProductsController.Create), new Type[] { typeof(ProductCreateEditViewModel) });
            Assert.NotNull(createMethod);
            Assert.Null(createMethod.GetCustomAttribute<AllowAnonymousAttribute>());

            var editMethod = typeof(ProductsController).GetMethod(nameof(ProductsController.Edit), new Type[] { typeof(ProductCreateEditViewModel) });
            Assert.NotNull(editMethod);
            Assert.Null(editMethod.GetCustomAttribute<AllowAnonymousAttribute>());

            var toggleMethod = typeof(ProductsController).GetMethod(nameof(ProductsController.ToggleActive), new Type[] { typeof(int) });
            Assert.NotNull(toggleMethod);
            Assert.Null(toggleMethod.GetCustomAttribute<AllowAnonymousAttribute>());
        }
    }
}
