using System;
using System.Collections.Generic;
using ward.Entities.Catalog;

namespace ward.Entities.Production
{
    public class BouquetRecipe
    {
        public int Id { get; set; }

        public int BouquetProductId { get; set; }
        public Product BouquetProduct { get; set; } = null!;

        public int Version { get; set; } = 1;
        public decimal LaborCost { get; set; } = 0;
        public decimal PackagingCost { get; set; } = 0;
        public string? Instructions { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        public ICollection<RecipeItem> Items { get; set; } = new List<RecipeItem>();
    }

    public class RecipeItem
    {
        public int Id { get; set; }

        public int BouquetRecipeId { get; set; }
        public BouquetRecipe BouquetRecipe { get; set; } = null!;

        public int ComponentProductId { get; set; }
        public Product ComponentProduct { get; set; } = null!;

        public decimal QuantityRequired { get; set; }
        public decimal WasteMarginPercentage { get; set; } = 0; // e.g. 5.0 for 5%
    }

    public class RecipeSnapshot
    {
        public int Id { get; set; }

        public int BouquetProductId { get; set; }
        public Product BouquetProduct { get; set; } = null!;

        public int Version { get; set; } = 1;
        public decimal LaborCost { get; set; }
        public decimal PackagingCost { get; set; }
        public decimal ComponentCostTotal { get; set; }
        public decimal TotalUnitCost { get; set; }
        public string ItemsSnapshotJson { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime SnapshotDateUtc { get; set; } = DateTime.UtcNow;
        public string? CreatedByUserId { get; set; }
    }
}
