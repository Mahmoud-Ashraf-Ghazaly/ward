using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Models.Catalog;
using ward.Services.Barcode;

namespace ward.Services.Catalog
{
    public interface IProductService
    {
        Task<ProductListFilterViewModel> GetPagedProductsAsync(ProductListFilterViewModel filter);
        Task<ProductDetailsViewModel?> GetDetailsByIdAsync(int id);
        Task<ProductCreateEditViewModel?> GetForEditAsync(int id);
        Task<(bool Success, string? Error, int ProductId)> CreateAsync(ProductCreateEditViewModel model);
        Task<(bool Success, string? Error)> UpdateAsync(ProductCreateEditViewModel model);
        Task<(bool Success, string? Error)> ToggleActiveAsync(int id);
        Task<ProductLookupDto?> LookupBySkuOrBarcodeAsync(string identifier);
        Task<bool> IsSkuUniqueAsync(string sku, int? excludeId = null);
        Task<bool> IsBarcodeUniqueAsync(string barcode, int? excludeId = null);
        Task<ProductBarcodeLabelViewModel?> GetBarcodeLabelAsync(int id, int count = 1);
    }

    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBarcodeService _barcodeService;

        public ProductService(ApplicationDbContext context, IBarcodeService barcodeService)
        {
            _context = context;
            _barcodeService = barcodeService;
        }

        public async Task<ProductListFilterViewModel> GetPagedProductsAsync(ProductListFilterViewModel filter)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.UnitOfMeasure)
                .Include(p => p.StockItems)
                .AsNoTracking();

            if (filter.OnlyActive.HasValue && filter.OnlyActive.Value)
            {
                query = query.Where(p => p.IsActive);
            }

            if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == filter.CategoryId.Value);
            }

            if (filter.ProductType.HasValue)
            {
                query = query.Where(p => p.ProductType == filter.ProductType.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.Trim().ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(search) ||
                    p.SKU.ToLower().Contains(search) ||
                    (p.Barcode != null && p.Barcode.ToLower().Contains(search)));
            }

            filter.TotalItems = await query.CountAsync();

            int pageSize = filter.PageSize > 0 ? filter.PageSize : 10;
            int pageNumber = filter.PageNumber > 0 ? filter.PageNumber : 1;

            var items = await query
                .OrderBy(p => p.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductListItemViewModel
                {
                    Id = p.Id,
                    SKU = p.SKU,
                    Barcode = p.Barcode,
                    Name = p.Name,
                    CategoryName = p.Category.Name,
                    UnitOfMeasureSymbol = p.UnitOfMeasure.Symbol,
                    UnitPrice = p.UnitPrice,
                    CurrentAverageCost = p.CurrentAverageCost,
                    MinimumStockLevel = p.MinimumStockLevel,
                    TotalStockOnHand = p.StockItems.Sum(s => s.QuantityOnHand),
                    ProductType = p.ProductType,
                    IsActive = p.IsActive
                })
                .ToListAsync();

            filter.Items = items;
            return filter;
        }

        public async Task<ProductDetailsViewModel?> GetDetailsByIdAsync(int id)
        {
            var p = await _context.Products
                .Include(prod => prod.Category)
                .Include(prod => prod.UnitOfMeasure)
                .Include(prod => prod.StockItems)
                .AsNoTracking()
                .FirstOrDefaultAsync(prod => prod.Id == id);

            if (p == null) return null;

            string? barcodeSvg = null;
            var barcodeValue = !string.IsNullOrWhiteSpace(p.Barcode) ? p.Barcode : p.SKU;
            if (!string.IsNullOrWhiteSpace(barcodeValue))
            {
                barcodeSvg = _barcodeService.GenerateCode128Svg(barcodeValue);
            }

            return new ProductDetailsViewModel
            {
                Id = p.Id,
                SKU = p.SKU,
                Barcode = p.Barcode,
                Name = p.Name,
                Description = p.Description,
                ProductType = p.ProductType,
                CategoryName = p.Category.Name,
                UnitOfMeasureName = p.UnitOfMeasure.Name,
                UnitOfMeasureSymbol = p.UnitOfMeasure.Symbol,
                AllowDecimals = p.UnitOfMeasure.AllowDecimals,
                UnitPrice = p.UnitPrice,
                CurrentAverageCost = p.CurrentAverageCost,
                MinimumStockLevel = p.MinimumStockLevel,
                TotalStockOnHand = p.StockItems.Sum(s => s.QuantityOnHand),
                IsActive = p.IsActive,
                CreatedAtUtc = p.CreatedAtUtc,
                BarcodeSvg = barcodeSvg
            };
        }

        public async Task<ProductCreateEditViewModel?> GetForEditAsync(int id)
        {
            var p = await _context.Products.FindAsync(id);
            if (p == null) return null;

            return new ProductCreateEditViewModel
            {
                Id = p.Id,
                SKU = p.SKU,
                Barcode = p.Barcode,
                Name = p.Name,
                Description = p.Description,
                ProductType = p.ProductType,
                CategoryId = p.CategoryId,
                UnitOfMeasureId = p.UnitOfMeasureId,
                UnitPrice = p.UnitPrice,
                CurrentAverageCost = p.CurrentAverageCost,
                MinimumStockLevel = p.MinimumStockLevel,
                IsActive = p.IsActive,
                RowVersion = p.RowVersion
            };
        }

        public async Task<bool> IsSkuUniqueAsync(string sku, int? excludeId = null)
        {
            var trimmedSku = sku.Trim();
            var query = _context.Products.AsNoTracking();
            if (excludeId.HasValue)
            {
                query = query.Where(p => p.Id != excludeId.Value);
            }
            return !await query.AnyAsync(p => p.SKU.ToLower() == trimmedSku.ToLower());
        }

        public async Task<bool> IsBarcodeUniqueAsync(string barcode, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return true;

            var trimmedBarcode = barcode.Trim();
            var query = _context.Products.AsNoTracking();
            if (excludeId.HasValue)
            {
                query = query.Where(p => p.Id != excludeId.Value);
            }
            return !await query.AnyAsync(p => p.Barcode != null && p.Barcode.ToLower() == trimmedBarcode.ToLower());
        }

        public async Task<(bool Success, string? Error, int ProductId)> CreateAsync(ProductCreateEditViewModel model)
        {
            var trimmedSku = model.SKU.Trim().ToUpperInvariant();
            if (!await IsSkuUniqueAsync(trimmedSku))
            {
                return (false, $"The SKU '{trimmedSku}' is already assigned to another product.", 0);
            }

            string? trimmedBarcode = string.IsNullOrWhiteSpace(model.Barcode)
                ? _barcodeService.GenerateUniqueBarcode("FLW")
                : model.Barcode.Trim();

            if (!string.IsNullOrWhiteSpace(trimmedBarcode) && !await IsBarcodeUniqueAsync(trimmedBarcode))
            {
                return (false, $"The Barcode '{trimmedBarcode}' is already assigned to another product.", 0);
            }

            var category = await _context.Categories.FindAsync(model.CategoryId);
            if (category == null)
            {
                return (false, "The selected category does not exist.", 0);
            }

            var uom = await _context.UnitsOfMeasure.FindAsync(model.UnitOfMeasureId);
            if (uom == null)
            {
                return (false, "The selected unit of measure does not exist.", 0);
            }

            var product = new Product
            {
                SKU = trimmedSku,
                Barcode = trimmedBarcode,
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                ProductType = model.ProductType,
                CategoryId = model.CategoryId,
                UnitOfMeasureId = model.UnitOfMeasureId,
                UnitPrice = model.UnitPrice,
                CurrentAverageCost = model.CurrentAverageCost,
                MinimumStockLevel = model.MinimumStockLevel,
                IsActive = model.IsActive,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return (true, null, product.Id);
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(ProductCreateEditViewModel model)
        {
            var product = await _context.Products.FindAsync(model.Id);
            if (product == null)
            {
                return (false, "Product not found.");
            }

            var trimmedSku = model.SKU.Trim().ToUpperInvariant();
            if (!await IsSkuUniqueAsync(trimmedSku, model.Id))
            {
                return (false, $"The SKU '{trimmedSku}' is already assigned to another product.");
            }

            string? trimmedBarcode = string.IsNullOrWhiteSpace(model.Barcode) ? null : model.Barcode.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedBarcode) && !await IsBarcodeUniqueAsync(trimmedBarcode, model.Id))
            {
                return (false, $"The Barcode '{trimmedBarcode}' is already assigned to another product.");
            }

            // Verify Category and UOM
            var category = await _context.Categories.FindAsync(model.CategoryId);
            if (category == null) return (false, "The selected category does not exist.");

            var uom = await _context.UnitsOfMeasure.FindAsync(model.UnitOfMeasureId);
            if (uom == null) return (false, "The selected unit of measure does not exist.");

            // Concurrency check if RowVersion provided
            if (model.RowVersion != null)
            {
                _context.Entry(product).Property(p => p.RowVersion).OriginalValue = model.RowVersion;
            }

            product.SKU = trimmedSku;
            product.Barcode = trimmedBarcode;
            product.Name = model.Name.Trim();
            product.Description = model.Description?.Trim();
            product.ProductType = model.ProductType;
            product.CategoryId = model.CategoryId;
            product.UnitOfMeasureId = model.UnitOfMeasureId;
            product.UnitPrice = model.UnitPrice;
            product.CurrentAverageCost = model.CurrentAverageCost;
            product.MinimumStockLevel = model.MinimumStockLevel;
            product.IsActive = model.IsActive;

            try
            {
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, "This product was modified by another user while you were editing it. Please refresh and try again.");
            }
        }

        public async Task<(bool Success, string? Error)> ToggleActiveAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return (false, "Product not found.");

            product.IsActive = !product.IsActive;
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<ProductLookupDto?> LookupBySkuOrBarcodeAsync(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return null;

            var term = identifier.Trim().ToLower();
            var p = await _context.Products
                .Include(prod => prod.Category)
                .Include(prod => prod.UnitOfMeasure)
                .Include(prod => prod.StockItems)
                .AsNoTracking()
                .FirstOrDefaultAsync(prod =>
                    prod.IsActive &&
                    (prod.SKU.ToLower() == term || (prod.Barcode != null && prod.Barcode.ToLower() == term)));

            if (p == null) return null;

            return new ProductLookupDto
            {
                Id = p.Id,
                SKU = p.SKU,
                Barcode = p.Barcode,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
                CurrentAverageCost = p.CurrentAverageCost,
                UnitOfMeasureSymbol = p.UnitOfMeasure.Symbol,
                CategoryName = p.Category.Name,
                AvailableStock = p.StockItems.Sum(s => s.QuantityOnHand)
            };
        }

        public async Task<ProductBarcodeLabelViewModel?> GetBarcodeLabelAsync(int id, int count = 1)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return null;

            var barcodeValue = !string.IsNullOrWhiteSpace(product.Barcode) ? product.Barcode : product.SKU;
            var barcodeSvg = _barcodeService.GenerateCode128Svg(barcodeValue);

            return new ProductBarcodeLabelViewModel
            {
                ProductId = product.Id,
                ProductName = product.Name,
                SKU = product.SKU,
                Barcode = product.Barcode,
                UnitPrice = product.UnitPrice,
                BarcodeSvg = barcodeSvg,
                NumberOfLabels = count > 0 ? count : 1
            };
        }
    }
}
