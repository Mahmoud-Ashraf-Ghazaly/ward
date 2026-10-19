using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ward.Entities.Enums;

namespace ward.Models.Catalog
{
    public class CategoryFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class CategoryListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int ProductCount { get; set; }
    }

    public class ProductListFilterViewModel
    {
        public string? SearchTerm { get; set; }
        public int? CategoryId { get; set; }
        public ProductType? ProductType { get; set; }
        public bool? OnlyActive { get; set; } = true;

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages => (int)System.Math.Ceiling((double)TotalItems / (PageSize > 0 ? PageSize : 10));

        public List<ProductListItemViewModel> Items { get; set; } = new List<ProductListItemViewModel>();
    }

    public class ProductListItemViewModel
    {
        public int Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string UnitOfMeasureSymbol { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal CurrentAverageCost { get; set; }
        public decimal MinimumStockLevel { get; set; }
        public decimal TotalStockOnHand { get; set; }
        public ProductType ProductType { get; set; }
        public bool IsActive { get; set; }
    }

    public class ProductCreateEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "SKU is required.")]
        [StringLength(50, ErrorMessage = "SKU cannot exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "SKU may only contain letters, numbers, hyphens, and underscores.")]
        public string SKU { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Barcode cannot exceed 100 characters.")]
        public string? Barcode { get; set; }

        [Required(ErrorMessage = "Product name is required.")]
        [StringLength(200, ErrorMessage = "Product name cannot exceed 200 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Product type is required.")]
        public ProductType ProductType { get; set; } = ProductType.FreshFlower;

        [Required(ErrorMessage = "Category is required.")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Unit of measure is required.")]
        public int UnitOfMeasureId { get; set; }

        [Required(ErrorMessage = "Selling price is required.")]
        [Range(0.01, 1000000.00, ErrorMessage = "Selling price must be greater than 0.00.")]
        public decimal UnitPrice { get; set; }

        [Required(ErrorMessage = "Purchase cost is required.")]
        [Range(0.00, 1000000.00, ErrorMessage = "Purchase cost cannot be negative.")]
        public decimal CurrentAverageCost { get; set; }

        [Range(0.0000, 100000.0000, ErrorMessage = "Minimum stock level cannot be negative.")]
        public decimal MinimumStockLevel { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public byte[]? RowVersion { get; set; }
    }

    public class ProductDetailsViewModel
    {
        public int Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ProductType ProductType { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string UnitOfMeasureName { get; set; } = string.Empty;
        public string UnitOfMeasureSymbol { get; set; } = string.Empty;
        public bool AllowDecimals { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal CurrentAverageCost { get; set; }
        public decimal MinimumStockLevel { get; set; }
        public decimal TotalStockOnHand { get; set; }
        public bool IsActive { get; set; }
        public System.DateTime CreatedAtUtc { get; set; }
        public string? BarcodeSvg { get; set; }
    }

    public class ProductBarcodeLabelViewModel
    {
        public int ProductId { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string BarcodeSvg { get; set; } = string.Empty;
        public int NumberOfLabels { get; set; } = 1;
    }

    public class ProductLookupDto
    {
        public int Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal CurrentAverageCost { get; set; }
        public string UnitOfMeasureSymbol { get; set; } = string.Empty;
        public decimal AvailableStock { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }
}
