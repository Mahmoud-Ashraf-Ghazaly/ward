using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ward.Entities.Catalog;
using ward.Entities.Inventory;
using ward.Entities.Production;
using ward.Entities.Purchasing;
using ward.Entities.Sales;

namespace ward.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Warehouse> Warehouses => Set<Warehouse>();
        public DbSet<StockItem> StockItems => Set<StockItem>();
        public DbSet<StockMovement> StockMovements => Set<StockMovement>();
        public DbSet<Stocktaking> Stocktakings => Set<Stocktaking>();
        public DbSet<StocktakingLine> StocktakingLines => Set<StocktakingLine>();
        public DbSet<BouquetRecipe> BouquetRecipes => Set<BouquetRecipe>();
        public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();
        public DbSet<RecipeSnapshot> RecipeSnapshots => Set<RecipeSnapshot>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
        public DbSet<InvoiceReturn> InvoiceReturns => Set<InvoiceReturn>();
        public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<InvoiceItemComponentConsumption> InvoiceItemComponentConsumptions => Set<InvoiceItemComponentConsumption>();

        // Purchasing DbSets
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
        public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
        public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
        public DbSet<PurchaseReturnItem> PurchaseReturnItems => Set<PurchaseReturnItem>();
        public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 1. Decimal Precision Configurations
            foreach (var property in builder.Model.GetEntityTypes()
                         .SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                var propName = property.Name;
                if (propName.Contains("Quantity") || propName.Contains("Level"))
                {
                    property.SetPrecision(18);
                    property.SetScale(4);
                }
                else if (propName.Contains("Percentage") || propName.Contains("Margin"))
                {
                    property.SetPrecision(5);
                    property.SetScale(2);
                }
                else
                {
                    property.SetPrecision(18);
                    property.SetScale(2);
                }
            }

            // 2. Concurrency Tokens
            builder.Entity<Product>()
                .Property(p => p.RowVersion)
                .IsRowVersion();

            builder.Entity<StockItem>()
                .Property(s => s.RowVersion)
                .IsRowVersion();

            builder.Entity<Invoice>()
                .Property(i => i.RowVersion)
                .IsRowVersion();

            builder.Entity<PurchaseInvoice>()
                .Property(p => p.RowVersion)
                .IsRowVersion();

            // 3. Unique Indexes
            builder.Entity<Product>()
                .HasIndex(p => p.SKU)
                .IsUnique();

            builder.Entity<Product>()
                .HasIndex(p => p.Barcode)
                .IsUnique()
                .HasFilter("[Barcode] IS NOT NULL");

            builder.Entity<Warehouse>()
                .HasIndex(w => w.Code)
                .IsUnique();

            builder.Entity<StockItem>()
                .HasIndex(s => new { s.ProductId, s.WarehouseId })
                .IsUnique();

            builder.Entity<Invoice>()
                .HasIndex(i => i.InvoiceNumber)
                .IsUnique();

            builder.Entity<InvoiceReturn>()
                .HasIndex(r => r.ReturnNumber)
                .IsUnique();

            builder.Entity<PurchaseInvoice>()
                .HasIndex(p => p.InvoiceNumber)
                .IsUnique();

            builder.Entity<PurchaseReturn>()
                .HasIndex(pr => pr.ReturnNumber)
                .IsUnique();

            builder.Entity<BouquetRecipe>()
                .HasIndex(b => b.BouquetProductId)
                .IsUnique();

            builder.Entity<RecipeItem>()
                .HasIndex(r => new { r.BouquetRecipeId, r.ComponentProductId })
                .IsUnique();

            builder.Entity<Stocktaking>()
                .HasIndex(s => s.StocktakingNumber);

            builder.Entity<Invoice>()
                .HasIndex(i => i.IdempotencyToken);

            builder.Entity<InvoiceReturn>()
                .HasIndex(r => r.IdempotencyToken);

            // 4. Relationships and Delete Restrictions
            builder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Product>()
                .HasOne(p => p.UnitOfMeasure)
                .WithMany(u => u.Products)
                .HasForeignKey(p => p.UnitOfMeasureId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StockItem>()
                .HasOne(s => s.Product)
                .WithMany(p => p.StockItems)
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StockItem>()
                .HasOne(s => s.Warehouse)
                .WithMany(w => w.StockItems)
                .HasForeignKey(s => s.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StockMovement>()
                .HasOne(m => m.Product)
                .WithMany()
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StockMovement>()
                .HasOne(m => m.Warehouse)
                .WithMany(w => w.StockMovements)
                .HasForeignKey(m => m.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BouquetRecipe>()
                .HasOne(b => b.BouquetProduct)
                .WithOne(p => p.BouquetRecipe)
                .HasForeignKey<BouquetRecipe>(b => b.BouquetProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<RecipeItem>()
                .HasOne(r => r.BouquetRecipe)
                .WithMany(b => b.Items)
                .HasForeignKey(r => r.BouquetRecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RecipeItem>()
                .HasOne(r => r.ComponentProduct)
                .WithMany(p => p.RecipeItemsAsComponent)
                .HasForeignKey(r => r.ComponentProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<RecipeSnapshot>()
                .HasOne(s => s.BouquetProduct)
                .WithMany()
                .HasForeignKey(s => s.BouquetProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Invoice>()
                .HasOne(i => i.Customer)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<InvoiceItem>()
                .HasOne(i => i.Invoice)
                .WithMany(inv => inv.Items)
                .HasForeignKey(i => i.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<InvoiceItem>()
                .HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InvoiceItem>()
                .HasOne(i => i.Warehouse)
                .WithMany()
                .HasForeignKey(i => i.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InvoiceItemComponentConsumption>()
                .HasOne(c => c.InvoiceItem)
                .WithMany(i => i.ComponentConsumptions)
                .HasForeignKey(c => c.InvoiceItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<InvoiceItemComponentConsumption>()
                .HasOne(c => c.ComponentProduct)
                .WithMany()
                .HasForeignKey(c => c.ComponentProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InvoiceReturn>()
                .HasOne(r => r.Invoice)
                .WithMany(i => i.Returns)
                .HasForeignKey(r => r.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ReturnItem>()
                .HasOne(ri => ri.InvoiceReturn)
                .WithMany(r => r.Items)
                .HasForeignKey(ri => ri.InvoiceReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ReturnItem>()
                .HasOne(ri => ri.InvoiceItem)
                .WithMany(ii => ii.ReturnItems)
                .HasForeignKey(ri => ri.InvoiceItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(p => p.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Stocktaking>()
                .HasOne(s => s.Warehouse)
                .WithMany(w => w.Stocktakings)
                .HasForeignKey(s => s.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StocktakingLine>()
                .HasOne(sl => sl.Stocktaking)
                .WithMany(s => s.Lines)
                .HasForeignKey(sl => sl.StocktakingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StocktakingLine>()
                .HasOne(sl => sl.Product)
                .WithMany()
                .HasForeignKey(sl => sl.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // 5. Purchasing Relationships
            builder.Entity<PurchaseInvoice>()
                .HasOne(p => p.Supplier)
                .WithMany(s => s.Invoices)
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PurchaseInvoice>()
                .HasOne(p => p.Warehouse)
                .WithMany()
                .HasForeignKey(p => p.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PurchaseInvoiceItem>()
                .HasOne(pi => pi.PurchaseInvoice)
                .WithMany(p => p.Items)
                .HasForeignKey(pi => pi.PurchaseInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PurchaseInvoiceItem>()
                .HasOne(pi => pi.Product)
                .WithMany()
                .HasForeignKey(pi => pi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PurchaseReturn>()
                .HasOne(pr => pr.PurchaseInvoice)
                .WithMany(pi => pi.Returns)
                .HasForeignKey(pr => pr.PurchaseInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PurchaseReturn>()
                .HasOne(pr => pr.Supplier)
                .WithMany()
                .HasForeignKey(pr => pr.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PurchaseReturn>()
                .HasOne(pr => pr.Warehouse)
                .WithMany()
                .HasForeignKey(pr => pr.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PurchaseReturnItem>()
                .HasOne(pri => pri.PurchaseReturn)
                .WithMany(pr => pr.Items)
                .HasForeignKey(pri => pri.PurchaseReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PurchaseReturnItem>()
                .HasOne(pri => pri.PurchaseInvoiceItem)
                .WithMany(pii => pii.ReturnItems)
                .HasForeignKey(pri => pri.PurchaseInvoiceItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PurchaseReturnItem>()
                .HasOne(pri => pri.Product)
                .WithMany()
                .HasForeignKey(pri => pri.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SupplierPayment>()
                .HasOne(sp => sp.Supplier)
                .WithMany(s => s.Payments)
                .HasForeignKey(sp => sp.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SupplierPayment>()
                .HasOne(sp => sp.PurchaseInvoice)
                .WithMany(pi => pi.Payments)
                .HasForeignKey(sp => sp.PurchaseInvoiceId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
