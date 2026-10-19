using System;
using System.Collections.Generic;
using ward.Entities.Enums;
using ward.Entities.Inventory;
using ward.Entities.Production;

namespace ward.Entities.Catalog
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    public class UnitOfMeasure
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // e.g., Stem, Bunch, Meter, Piece
        public string Symbol { get; set; } = string.Empty; // e.g., stm, bn, m, pc
        public bool AllowDecimals { get; set; } = false;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    public class Product
    {
        public int Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ProductType ProductType { get; set; } = ProductType.FreshFlower;

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        public int UnitOfMeasureId { get; set; }
        public UnitOfMeasure UnitOfMeasure { get; set; } = null!;

        public decimal UnitPrice { get; set; }
        public decimal CurrentAverageCost { get; set; }
        public decimal MinimumStockLevel { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public byte[]? RowVersion { get; set; }

        // Navigation properties
        public BouquetRecipe? BouquetRecipe { get; set; }
        public ICollection<StockItem> StockItems { get; set; } = new List<StockItem>();
        public ICollection<RecipeItem> RecipeItemsAsComponent { get; set; } = new List<RecipeItem>();
    }
}
