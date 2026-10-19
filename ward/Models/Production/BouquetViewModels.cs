using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ward.Models.Production
{
    public class BouquetRecipeListItemViewModel
    {
        public int RecipeId { get; set; }
        public int BouquetProductId { get; set; }
        public string BouquetProductName { get; set; } = string.Empty;
        public string BouquetSKU { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal GrossMargin => SellingPrice > 0 ? ((SellingPrice - EstimatedCost) / SellingPrice) * 100 : 0;
        public int ComponentCount { get; set; }
        public int Version { get; set; }
        public bool IsActive { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    public class RecipeItemFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Component product is required.")]
        public int ComponentProductId { get; set; }
        public string ComponentProductName { get; set; } = string.Empty;
        public string ComponentSKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = string.Empty;

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(0.0001, 10000.0000, ErrorMessage = "Quantity must be greater than zero.")]
        public decimal QuantityRequired { get; set; }

        [Range(0.00, 100.00, ErrorMessage = "Waste margin must be between 0% and 100%.")]
        public decimal WasteMarginPercentage { get; set; } = 0;

        public decimal UnitCost { get; set; }
        public decimal EffectiveQuantity => QuantityRequired * (1 + (WasteMarginPercentage / 100m));
        public decimal EstimatedItemCost => EffectiveQuantity * UnitCost;
    }

    public class BouquetRecipeFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Bouquet product is required.")]
        public int BouquetProductId { get; set; }
        public string BouquetProductName { get; set; } = string.Empty;
        public string BouquetSKU { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }

        [Range(0.00, 10000.00, ErrorMessage = "Labor cost cannot be negative.")]
        public decimal LaborCost { get; set; } = 0;

        [Range(0.00, 10000.00, ErrorMessage = "Packaging cost cannot be negative.")]
        public decimal PackagingCost { get; set; } = 0;

        [StringLength(1000, ErrorMessage = "Instructions cannot exceed 1000 characters.")]
        public string? Instructions { get; set; }

        public bool IsActive { get; set; } = true;
        public int Version { get; set; } = 1;

        [StringLength(250, ErrorMessage = "Snapshot notes cannot exceed 250 characters.")]
        public string? SnapshotNotes { get; set; }

        public List<RecipeItemFormViewModel> Items { get; set; } = new List<RecipeItemFormViewModel>();
    }

    public class RecipeCalculationItemDto
    {
        public int ComponentProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = string.Empty;
        public decimal QuantityPerBouquet { get; set; }
        public decimal WasteMarginPercentage { get; set; }
        public decimal EffectiveQuantityPerBouquet { get; set; }
        public decimal TotalQuantityRequired { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalComponentCost { get; set; }
    }

    public class RecipeCalculationResultDto
    {
        public int BouquetProductId { get; set; }
        public string BouquetProductName { get; set; } = string.Empty;
        public decimal RequestedBouquetQuantity { get; set; }
        public decimal UnitLaborCost { get; set; }
        public decimal TotalLaborCost => UnitLaborCost * RequestedBouquetQuantity;
        public decimal UnitPackagingCost { get; set; }
        public decimal TotalPackagingCost => UnitPackagingCost * RequestedBouquetQuantity;
        public decimal UnitComponentsCost { get; set; }
        public decimal TotalComponentsCost => UnitComponentsCost * RequestedBouquetQuantity;
        public decimal EstimatedUnitCost => UnitComponentsCost + UnitLaborCost + UnitPackagingCost;
        public decimal TotalEstimatedCost => EstimatedUnitCost * RequestedBouquetQuantity;
        public List<RecipeCalculationItemDto> Components { get; set; } = new List<RecipeCalculationItemDto>();
    }

    public class ComponentAvailabilityItemDto
    {
        public int ComponentProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string UnitSymbol { get; set; } = string.Empty;
        public decimal QuantityRequired { get; set; }
        public decimal AvailableStock { get; set; }
        public bool IsSufficient => AvailableStock >= QuantityRequired;
        public decimal ShortageQuantity => IsSufficient ? 0 : QuantityRequired - AvailableStock;
    }

    public class ComponentAvailabilityResultDto
    {
        public int BouquetProductId { get; set; }
        public string BouquetProductName { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public decimal RequestedQuantity { get; set; }
        public bool AllAvailable { get; set; }
        public List<ComponentAvailabilityItemDto> Components { get; set; } = new List<ComponentAvailabilityItemDto>();
    }

    public class AssembleBouquetsViewModel
    {
        [Required(ErrorMessage = "Bouquet product is required.")]
        public int BouquetProductId { get; set; }

        [Required(ErrorMessage = "Warehouse is required.")]
        public int WarehouseId { get; set; }

        [Required(ErrorMessage = "Quantity to assemble is required.")]
        [Range(1, 10000, ErrorMessage = "Assembly quantity must be at least 1.")]
        public int QuantityToAssemble { get; set; } = 1;

        [StringLength(250, ErrorMessage = "Notes cannot exceed 250 characters.")]
        public string? Notes { get; set; }

        [Required]
        public string IdempotencyToken { get; set; } = Guid.NewGuid().ToString();
    }

    public class RecipeSnapshotDto
    {
        public int Id { get; set; }
        public int Version { get; set; }
        public DateTime SnapshotDateUtc { get; set; }
        public decimal LaborCost { get; set; }
        public decimal PackagingCost { get; set; }
        public decimal ComponentCostTotal { get; set; }
        public decimal TotalUnitCost { get; set; }
        public string? Notes { get; set; }
        public string? CreatedByUserId { get; set; }
        public string ItemsSnapshotJson { get; set; } = string.Empty;
    }
}
