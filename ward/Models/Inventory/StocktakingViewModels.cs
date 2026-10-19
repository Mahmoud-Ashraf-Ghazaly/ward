using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ward.Entities.Enums;

namespace ward.Models.Inventory
{
    public class StocktakingListItemViewModel
    {
        public int Id { get; set; }
        public string StocktakingNumber { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime StartDateUtc { get; set; }
        public DateTime? CompletedDateUtc { get; set; }
        public DateTime? ApprovedDateUtc { get; set; }
        public StocktakingStatus Status { get; set; }
        public int LinesCount { get; set; }
        public decimal TotalShortageQuantity { get; set; }
        public decimal TotalSurplusQuantity { get; set; }
        public decimal NetDifferenceQuantity { get; set; }
        public decimal TotalVarianceCost { get; set; }
        public string? Reason { get; set; }
        public string? CreatedByUserId { get; set; }
        public string? ApprovedByUserId { get; set; }
    }

    public class StocktakingCreateViewModel
    {
        [Required(ErrorMessage = "Warehouse selection is required.")]
        public int WarehouseId { get; set; }

        [StringLength(250, ErrorMessage = "Reason cannot exceed 250 characters.")]
        public string? Reason { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }
    }

    public class StocktakingLineInputDto
    {
        public int LineId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = "pcs";
        public string CategoryName { get; set; } = string.Empty;

        public decimal SystemQuantity { get; set; }

        [Range(0, 1000000, ErrorMessage = "Counted quantity cannot be negative.")]
        public decimal CountedQuantity { get; set; }

        public decimal DifferenceQuantity => CountedQuantity - SystemQuantity;

        public decimal CurrentSystemQuantity { get; set; }
        public decimal ConcurrentMovementDelta => CurrentSystemQuantity - SystemQuantity;
        public bool HasConcurrentMovements => Math.Abs(ConcurrentMovementDelta) > 0.0001m;

        public decimal UnitCost { get; set; }
        public decimal VarianceCost => Math.Round(DifferenceQuantity * UnitCost, 2);

        [StringLength(200, ErrorMessage = "Reason cannot exceed 200 characters.")]
        public string? Reason { get; set; }
    }

    public class StocktakingCountViewModel
    {
        public int Id { get; set; }
        public string StocktakingNumber { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime StartDateUtc { get; set; }
        public StocktakingStatus Status { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }

        public List<StocktakingLineInputDto> Lines { get; set; } = new List<StocktakingLineInputDto>();
    }

    public class StocktakingReviewViewModel
    {
        public int Id { get; set; }
        public string StocktakingNumber { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime StartDateUtc { get; set; }
        public StocktakingStatus Status { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public string? CreatedByUserId { get; set; }

        public decimal TotalShortageQuantity { get; set; }
        public decimal TotalSurplusQuantity { get; set; }
        public decimal NetDifferenceQuantity { get; set; }
        public decimal TotalVarianceCost { get; set; }

        public string IdempotencyToken { get; set; } = Guid.NewGuid().ToString();

        [StringLength(500, ErrorMessage = "Approval notes cannot exceed 500 characters.")]
        public string? ApprovalNotes { get; set; }

        public List<StocktakingLineInputDto> Lines { get; set; } = new List<StocktakingLineInputDto>();
    }

    public class StocktakingDetailsViewModel
    {
        public int Id { get; set; }
        public string StocktakingNumber { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public DateTime StartDateUtc { get; set; }
        public DateTime? CompletedDateUtc { get; set; }
        public DateTime? ApprovedDateUtc { get; set; }
        public StocktakingStatus Status { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public string? CreatedByUserId { get; set; }
        public string? ApprovedByUserId { get; set; }

        public decimal TotalShortageQuantity { get; set; }
        public decimal TotalSurplusQuantity { get; set; }
        public decimal NetDifferenceQuantity { get; set; }
        public decimal TotalVarianceCost { get; set; }

        public List<StocktakingLineInputDto> Lines { get; set; } = new List<StocktakingLineInputDto>();
    }
}
