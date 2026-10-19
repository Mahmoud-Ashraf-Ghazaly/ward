using System;
using System.Collections.Generic;
using ward.Entities.Catalog;
using ward.Entities.Enums;

namespace ward.Entities.Inventory
{
    public class Warehouse
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty; // e.g. "MAIN", "COLD-STORE"
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;

        public ICollection<StockItem> StockItems { get; set; } = new List<StockItem>();
        public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
        public ICollection<Stocktaking> Stocktakings { get; set; } = new List<Stocktaking>();
    }

    public class StockItem
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = null!;

        public decimal QuantityOnHand { get; set; } = 0;
        public decimal QuantityReserved { get; set; } = 0;

        public byte[]? RowVersion { get; set; }
    }

    public class StockMovement
    {
        public long Id { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = null!;

        public MovementType MovementType { get; set; }
        public decimal Quantity { get; set; } // Positive for inbound, negative for outbound
        public decimal UnitCost { get; set; }

        public string ReferenceType { get; set; } = string.Empty; // e.g., "Invoice", "Stocktaking", "Return", "Production"
        public string? ReferenceId { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string? CreatedByUserId { get; set; }
    }

    public class Stocktaking
    {
        public int Id { get; set; }
        public string StocktakingNumber { get; set; } = string.Empty;

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = null!;

        public DateTime StartDateUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedDateUtc { get; set; }
        public DateTime? ApprovedDateUtc { get; set; }
        public string? CreatedByUserId { get; set; }
        public string? ApprovedByUserId { get; set; }

        public StocktakingStatus Status { get; set; } = StocktakingStatus.Draft;
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public string? IdempotencyToken { get; set; }

        public decimal TotalShortageQuantity { get; set; } = 0;
        public decimal TotalSurplusQuantity { get; set; } = 0;
        public decimal NetDifferenceQuantity { get; set; } = 0;
        public decimal TotalVarianceCost { get; set; } = 0;

        public ICollection<StocktakingLine> Lines { get; set; } = new List<StocktakingLine>();
    }

    public class StocktakingLine
    {
        public int Id { get; set; }

        public int StocktakingId { get; set; }
        public Stocktaking Stocktaking { get; set; } = null!;

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public decimal SystemQuantity { get; set; }
        public decimal CountedQuantity { get; set; }
        public decimal DifferenceQuantity { get; set; }
        public decimal CurrentSystemQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal VarianceCost { get; set; }
        public string? Reason { get; set; }
    }
}
