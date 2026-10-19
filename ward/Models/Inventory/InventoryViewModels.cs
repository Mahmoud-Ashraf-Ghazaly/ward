using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ward.Entities.Enums;

namespace ward.Models.Inventory
{
    public class WarehouseFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Warehouse code is required.")]
        [StringLength(50, ErrorMessage = "Warehouse code cannot exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Warehouse code can only contain letters, numbers, hyphens, and underscores.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Warehouse name is required.")]
        [StringLength(150, ErrorMessage = "Warehouse name cannot exceed 150 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(250, ErrorMessage = "Location cannot exceed 250 characters.")]
        public string? Location { get; set; }

        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;
    }

    public class WarehouseListItemViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int DistinctItemCount { get; set; }
        public decimal TotalQuantityOnHand { get; set; }
    }

    public enum StockStatusFilter
    {
        All = 0,
        InStock = 1,
        LowStock = 2,
        OutOfStock = 3
    }

    public class WarehouseStockFilterViewModel
    {
        public int? WarehouseId { get; set; }
        public string? SearchTerm { get; set; }
        public int? CategoryId { get; set; }
        public ProductType? ProductType { get; set; }
        public StockStatusFilter StatusFilter { get; set; } = StockStatusFilter.All;

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / (PageSize > 0 ? PageSize : 15));

        public List<StockItemViewModel> Items { get; set; } = new List<StockItemViewModel>();
        public decimal TotalValuation { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
    }

    public class StockItemViewModel
    {
        public int StockItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = string.Empty;
        public ProductType ProductType { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string WarehouseCode { get; set; } = string.Empty;
        public decimal QuantityOnHand { get; set; }
        public decimal MinimumStockLevel { get; set; }
        public decimal CurrentAverageCost { get; set; }
        public decimal Valuation => QuantityOnHand * CurrentAverageCost;
        public bool IsOutOfStock => QuantityOnHand <= 0;
        public bool IsLowStock => QuantityOnHand > 0 && QuantityOnHand <= MinimumStockLevel;
    }

    public class StockMovementFilterViewModel
    {
        public int? WarehouseId { get; set; }
        public int? ProductId { get; set; }
        public MovementType? MovementType { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? ReferenceQuery { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / (PageSize > 0 ? PageSize : 20));

        public List<StockMovementItemViewModel> Items { get; set; } = new List<StockMovementItemViewModel>();
    }

    public class StockMovementItemViewModel
    {
        public long Id { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string WarehouseCode { get; set; } = string.Empty;
        public MovementType MovementType { get; set; }
        public decimal Quantity { get; set; }
        public string UnitSymbol { get; set; } = string.Empty;
        public decimal UnitCost { get; set; }
        public decimal TotalCost => Math.Abs(Quantity) * UnitCost;
        public string ReferenceType { get; set; } = string.Empty;
        public string? ReferenceId { get; set; }
        public string? Notes { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class ReceiveStockViewModel
    {
        [Required(ErrorMessage = "Product is required.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Destination warehouse is required.")]
        public int WarehouseId { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(0.0001, 1000000.0000, ErrorMessage = "Quantity must be greater than zero.")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Unit cost is required.")]
        [Range(0.00, 1000000.00, ErrorMessage = "Unit cost cannot be negative.")]
        public decimal UnitCost { get; set; }

        [Required(ErrorMessage = "Reference / Supplier invoice number is required for audit.")]
        [StringLength(100, ErrorMessage = "Reference cannot exceed 100 characters.")]
        public string ReferenceNumber { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }

        [Required]
        public string IdempotencyToken { get; set; } = Guid.NewGuid().ToString();
    }

    public class IssueStockViewModel
    {
        [Required(ErrorMessage = "Product is required.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Warehouse is required.")]
        public int WarehouseId { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(0.0001, 1000000.0000, ErrorMessage = "Quantity must be greater than zero.")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Reason is required.")]
        public MovementType ReasonType { get; set; } = MovementType.DamagedDiscardOut; // DamagedDiscardOut, etc.

        [Required(ErrorMessage = "Reason explanation / authorization notes required for audit.")]
        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string Notes { get; set; } = string.Empty;

        [Required]
        public string IdempotencyToken { get; set; } = Guid.NewGuid().ToString();
    }

    public class TransferStockViewModel
    {
        [Required(ErrorMessage = "Product is required.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Source warehouse is required.")]
        public int SourceWarehouseId { get; set; }

        [Required(ErrorMessage = "Destination warehouse is required.")]
        public int DestinationWarehouseId { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(0.0001, 1000000.0000, ErrorMessage = "Quantity must be greater than zero.")]
        public decimal Quantity { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }

        [Required]
        public string IdempotencyToken { get; set; } = Guid.NewGuid().ToString();
    }

    public class AdjustStockViewModel
    {
        [Required(ErrorMessage = "Product is required.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Warehouse is required.")]
        public int WarehouseId { get; set; }

        [Required(ErrorMessage = "New counted quantity is required.")]
        [Range(0.0000, 1000000.0000, ErrorMessage = "Quantity cannot be negative.")]
        public decimal NewQuantityOnHand { get; set; }

        [Required(ErrorMessage = "Reason for adjustment is required.")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
        public string Reason { get; set; } = string.Empty;

        [Required]
        public string IdempotencyToken { get; set; } = Guid.NewGuid().ToString();
    }

    public class InventoryOperationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public decimal NewQuantityOnHand { get; set; }
        public List<long> MovementIds { get; set; } = new List<long>();

        public static InventoryOperationResult Ok(decimal newQty, params long[] movementIds) =>
            new InventoryOperationResult { Success = true, NewQuantityOnHand = newQty, MovementIds = new List<long>(movementIds) };

        public static InventoryOperationResult Fail(string error) =>
            new InventoryOperationResult { Success = false, ErrorMessage = error };
    }
}
