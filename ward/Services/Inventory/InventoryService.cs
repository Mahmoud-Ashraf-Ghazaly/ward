using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ward.Data;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Entities.Inventory;
using ward.Models.Inventory;

namespace ward.Services.Inventory
{
    public interface IInventoryService
    {
        Task<WarehouseStockFilterViewModel> GetWarehouseStockPagedAsync(WarehouseStockFilterViewModel filter);
        Task<StockMovementFilterViewModel> GetStockMovementsPagedAsync(StockMovementFilterViewModel filter);
        Task<InventoryOperationResult> ReceiveStockAsync(ReceiveStockViewModel request, string? userId);
        Task<InventoryOperationResult> IssueStockAsync(IssueStockViewModel request, string? userId);
        Task<InventoryOperationResult> TransferStockAsync(TransferStockViewModel request, string? userId);
        Task<InventoryOperationResult> AdjustStockAsync(AdjustStockViewModel request, string? userId);
        Task<decimal> GetProductStockInWarehouseAsync(int productId, int warehouseId);
        Task<decimal> GetProductTotalStockAsync(int productId);
        Task<bool> VerifyStockIntegrityAsync(int productId, int warehouseId);
    }

    public class InventoryService : IInventoryService
    {
        private readonly ApplicationDbContext _context;

        public InventoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<WarehouseStockFilterViewModel> GetWarehouseStockPagedAsync(WarehouseStockFilterViewModel filter)
        {
            var query = _context.StockItems
                .Include(s => s.Product)
                    .ThenInclude(p => p.Category)
                .Include(s => s.Product)
                    .ThenInclude(p => p.UnitOfMeasure)
                .Include(s => s.Warehouse)
                .AsNoTracking();

            if (filter.WarehouseId.HasValue && filter.WarehouseId.Value > 0)
            {
                query = query.Where(s => s.WarehouseId == filter.WarehouseId.Value);
            }

            if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0)
            {
                query = query.Where(s => s.Product.CategoryId == filter.CategoryId.Value);
            }

            if (filter.ProductType.HasValue)
            {
                query = query.Where(s => s.Product.ProductType == filter.ProductType.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var search = filter.SearchTerm.Trim().ToLower();
                query = query.Where(s =>
                    s.Product.Name.ToLower().Contains(search) ||
                    s.Product.SKU.ToLower().Contains(search) ||
                    (s.Product.Barcode != null && s.Product.Barcode.ToLower().Contains(search)));
            }

            if (filter.StatusFilter == StockStatusFilter.InStock)
            {
                query = query.Where(s => s.QuantityOnHand > s.Product.MinimumStockLevel);
            }
            else if (filter.StatusFilter == StockStatusFilter.LowStock)
            {
                query = query.Where(s => s.QuantityOnHand > 0 && s.QuantityOnHand <= s.Product.MinimumStockLevel);
            }
            else if (filter.StatusFilter == StockStatusFilter.OutOfStock)
            {
                query = query.Where(s => s.QuantityOnHand <= 0);
            }

            filter.TotalItems = await query.CountAsync();
            var valuationItems = await query.Select(s => new { s.QuantityOnHand, s.Product.CurrentAverageCost }).ToListAsync();
            filter.TotalValuation = valuationItems.Sum(v => v.QuantityOnHand * v.CurrentAverageCost);
            filter.LowStockCount = await query.CountAsync(s => s.QuantityOnHand > 0 && s.QuantityOnHand <= s.Product.MinimumStockLevel);
            filter.OutOfStockCount = await query.CountAsync(s => s.QuantityOnHand <= 0);

            int pageSize = filter.PageSize > 0 ? filter.PageSize : 15;
            int pageNumber = filter.PageNumber > 0 ? filter.PageNumber : 1;

            filter.Items = await query
                .OrderBy(s => s.Product.Name)
                .ThenBy(s => s.Warehouse.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new StockItemViewModel
                {
                    StockItemId = s.Id,
                    ProductId = s.ProductId,
                    ProductName = s.Product.Name,
                    SKU = s.Product.SKU,
                    Barcode = s.Product.Barcode,
                    CategoryName = s.Product.Category.Name,
                    UnitSymbol = s.Product.UnitOfMeasure.Symbol,
                    ProductType = s.Product.ProductType,
                    WarehouseId = s.WarehouseId,
                    WarehouseName = s.Warehouse.Name,
                    WarehouseCode = s.Warehouse.Code,
                    QuantityOnHand = s.QuantityOnHand,
                    MinimumStockLevel = s.Product.MinimumStockLevel,
                    CurrentAverageCost = s.Product.CurrentAverageCost
                })
                .ToListAsync();

            return filter;
        }

        public async Task<StockMovementFilterViewModel> GetStockMovementsPagedAsync(StockMovementFilterViewModel filter)
        {
            var query = _context.StockMovements
                .Include(m => m.Product)
                    .ThenInclude(p => p.UnitOfMeasure)
                .Include(m => m.Warehouse)
                .AsNoTracking();

            if (filter.WarehouseId.HasValue && filter.WarehouseId.Value > 0)
            {
                query = query.Where(m => m.WarehouseId == filter.WarehouseId.Value);
            }

            if (filter.ProductId.HasValue && filter.ProductId.Value > 0)
            {
                query = query.Where(m => m.ProductId == filter.ProductId.Value);
            }

            if (filter.MovementType.HasValue)
            {
                query = query.Where(m => m.MovementType == filter.MovementType.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(m => m.CreatedAtUtc >= filter.FromDate.Value.ToUniversalTime());
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(m => m.CreatedAtUtc <= filter.ToDate.Value.ToUniversalTime().AddDays(1));
            }

            if (!string.IsNullOrWhiteSpace(filter.ReferenceQuery))
            {
                var refQ = filter.ReferenceQuery.Trim().ToLower();
                query = query.Where(m =>
                    (m.ReferenceId != null && m.ReferenceId.ToLower().Contains(refQ)) ||
                    (m.Notes != null && m.Notes.ToLower().Contains(refQ)) ||
                    m.Product.SKU.ToLower().Contains(refQ) ||
                    m.Product.Name.ToLower().Contains(refQ));
            }

            filter.TotalItems = await query.CountAsync();

            int pageSize = filter.PageSize > 0 ? filter.PageSize : 20;
            int pageNumber = filter.PageNumber > 0 ? filter.PageNumber : 1;

            filter.Items = await query
                .OrderByDescending(m => m.CreatedAtUtc)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new StockMovementItemViewModel
                {
                    Id = m.Id,
                    CreatedAtUtc = m.CreatedAtUtc,
                    ProductId = m.ProductId,
                    ProductName = m.Product.Name,
                    SKU = m.Product.SKU,
                    WarehouseId = m.WarehouseId,
                    WarehouseName = m.Warehouse.Name,
                    WarehouseCode = m.Warehouse.Code,
                    MovementType = m.MovementType,
                    Quantity = m.Quantity,
                    UnitSymbol = m.Product.UnitOfMeasure.Symbol,
                    UnitCost = m.UnitCost,
                    ReferenceType = m.ReferenceType,
                    ReferenceId = m.ReferenceId,
                    Notes = m.Notes,
                    CreatedBy = m.CreatedByUserId
                })
                .ToListAsync();

            return filter;
        }

        public async Task<InventoryOperationResult> ReceiveStockAsync(ReceiveStockViewModel request, string? userId)
        {
            if (request.Quantity <= 0)
                return InventoryOperationResult.Fail("Received quantity must be greater than zero.");

            // Idempotency guard: prevent duplicate processing of same submission
            if (!string.IsNullOrWhiteSpace(request.IdempotencyToken))
            {
                var existingMovement = await _context.StockMovements
                    .FirstOrDefaultAsync(m => m.ReferenceId == request.IdempotencyToken);
                if (existingMovement != null)
                {
                    var existingStock = await _context.StockItems
                        .FirstOrDefaultAsync(s => s.ProductId == existingMovement.ProductId && s.WarehouseId == existingMovement.WarehouseId);
                    return InventoryOperationResult.Ok(existingStock?.QuantityOnHand ?? 0, existingMovement.Id);
                }
            }

            await using var transaction = (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null || !product.IsActive)
                    return InventoryOperationResult.Fail("Product not found or is inactive.");

                var warehouse = await _context.Warehouses.FindAsync(request.WarehouseId);
                if (warehouse == null || !warehouse.IsActive)
                    return InventoryOperationResult.Fail("Warehouse not found or is inactive.");

                // Calculate current total stock across all warehouses before mutation
                var currentStockList = await _context.StockItems
                    .Where(s => s.ProductId == request.ProductId)
                    .Select(s => s.QuantityOnHand)
                    .ToListAsync();
                var totalCurrentStock = currentStockList.Sum();

                var stockItem = await _context.StockItems
                    .FirstOrDefaultAsync(s => s.ProductId == request.ProductId && s.WarehouseId == request.WarehouseId);

                if (stockItem == null)
                {
                    stockItem = new StockItem
                    {
                        ProductId = request.ProductId,
                        WarehouseId = request.WarehouseId,
                        QuantityOnHand = 0,
                        QuantityReserved = 0
                    };
                    _context.StockItems.Add(stockItem);
                }

                if (totalCurrentStock + request.Quantity > 0)
                {
                    var totalValue = (totalCurrentStock * product.CurrentAverageCost) + (request.Quantity * request.UnitCost);
                    product.CurrentAverageCost = Math.Round(totalValue / (totalCurrentStock + request.Quantity), 2);
                }
                else
                {
                    product.CurrentAverageCost = request.UnitCost;
                }

                stockItem.QuantityOnHand += request.Quantity;

                var movement = new StockMovement
                {
                    ProductId = request.ProductId,
                    WarehouseId = request.WarehouseId,
                    MovementType = MovementType.PurchaseIn,
                    Quantity = request.Quantity,
                    UnitCost = request.UnitCost,
                    ReferenceType = "Receipt",
                    ReferenceId = request.IdempotencyToken,
                    Notes = $"Receipt from {request.ReferenceNumber}. {request.Notes}".Trim(),
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = userId
                };

                _context.StockMovements.Add(movement);
                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return InventoryOperationResult.Ok(stockItem.QuantityOnHand, movement.Id);
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return InventoryOperationResult.Fail($"Failed to process receipt: {ex.Message}");
            }
        }

        public async Task<InventoryOperationResult> IssueStockAsync(IssueStockViewModel request, string? userId)
        {
            if (request.Quantity <= 0)
                return InventoryOperationResult.Fail("Issued quantity must be greater than zero.");

            // Idempotency guard
            if (!string.IsNullOrWhiteSpace(request.IdempotencyToken))
            {
                var existingMovement = await _context.StockMovements
                    .FirstOrDefaultAsync(m => m.ReferenceId == request.IdempotencyToken);
                if (existingMovement != null)
                {
                    var existingStock = await _context.StockItems
                        .FirstOrDefaultAsync(s => s.ProductId == existingMovement.ProductId && s.WarehouseId == existingMovement.WarehouseId);
                    return InventoryOperationResult.Ok(existingStock?.QuantityOnHand ?? 0, existingMovement.Id);
                }
            }

            await using var transaction = (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                    return InventoryOperationResult.Fail("Product not found.");

                var warehouse = await _context.Warehouses.FindAsync(request.WarehouseId);
                if (warehouse == null)
                    return InventoryOperationResult.Fail("Warehouse not found.");

                var stockItem = await _context.StockItems
                    .FirstOrDefaultAsync(s => s.ProductId == request.ProductId && s.WarehouseId == request.WarehouseId);

                if (stockItem == null || stockItem.QuantityOnHand < request.Quantity)
                {
                    var available = stockItem?.QuantityOnHand ?? 0;
                    return InventoryOperationResult.Fail($"Insufficient stock in warehouse '{warehouse.Name}'. Available: {available:0.####}, requested: {request.Quantity:0.####}.");
                }

                stockItem.QuantityOnHand -= request.Quantity;

                var movement = new StockMovement
                {
                    ProductId = request.ProductId,
                    WarehouseId = request.WarehouseId,
                    MovementType = request.ReasonType,
                    Quantity = -request.Quantity, // Outbound is negative
                    UnitCost = product.CurrentAverageCost,
                    ReferenceType = "Issue",
                    ReferenceId = request.IdempotencyToken,
                    Notes = request.Notes?.Trim(),
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = userId
                };

                _context.StockMovements.Add(movement);
                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return InventoryOperationResult.Ok(stockItem.QuantityOnHand, movement.Id);
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return InventoryOperationResult.Fail($"Failed to issue stock: {ex.Message}");
            }
        }

        public async Task<InventoryOperationResult> TransferStockAsync(TransferStockViewModel request, string? userId)
        {
            if (request.Quantity <= 0)
                return InventoryOperationResult.Fail("Transfer quantity must be greater than zero.");

            if (request.SourceWarehouseId == request.DestinationWarehouseId)
                return InventoryOperationResult.Fail("Source and destination warehouses cannot be the same.");

            // Idempotency guard
            var idempotencyOut = request.IdempotencyToken + "-OUT";
            var existingOut = await _context.StockMovements
                .FirstOrDefaultAsync(m => m.ReferenceId == idempotencyOut);
            if (existingOut != null)
            {
                var currentSourceStock = await _context.StockItems
                    .FirstOrDefaultAsync(s => s.ProductId == request.ProductId && s.WarehouseId == request.SourceWarehouseId);
                return InventoryOperationResult.Ok(currentSourceStock?.QuantityOnHand ?? 0, existingOut.Id);
            }

            await using var transaction = (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                    return InventoryOperationResult.Fail("Product not found.");

                var sourceWarehouse = await _context.Warehouses.FindAsync(request.SourceWarehouseId);
                if (sourceWarehouse == null || !sourceWarehouse.IsActive)
                    return InventoryOperationResult.Fail("Source warehouse not found or is inactive.");

                var destWarehouse = await _context.Warehouses.FindAsync(request.DestinationWarehouseId);
                if (destWarehouse == null || !destWarehouse.IsActive)
                    return InventoryOperationResult.Fail("Destination warehouse not found or is inactive.");

                var sourceStock = await _context.StockItems
                    .FirstOrDefaultAsync(s => s.ProductId == request.ProductId && s.WarehouseId == request.SourceWarehouseId);

                if (sourceStock == null || sourceStock.QuantityOnHand < request.Quantity)
                {
                    var available = sourceStock?.QuantityOnHand ?? 0;
                    return InventoryOperationResult.Fail($"Insufficient stock in source warehouse '{sourceWarehouse.Name}'. Available: {available:0.####}, requested: {request.Quantity:0.####}.");
                }

                var destStock = await _context.StockItems
                    .FirstOrDefaultAsync(s => s.ProductId == request.ProductId && s.WarehouseId == request.DestinationWarehouseId);

                if (destStock == null)
                {
                    destStock = new StockItem
                    {
                        ProductId = request.ProductId,
                        WarehouseId = request.DestinationWarehouseId,
                        QuantityOnHand = 0,
                        QuantityReserved = 0
                    };
                    _context.StockItems.Add(destStock);
                }

                // Atomically adjust source and destination
                sourceStock.QuantityOnHand -= request.Quantity;
                destStock.QuantityOnHand += request.Quantity;

                // Create Outbound Movement
                var movementOut = new StockMovement
                {
                    ProductId = request.ProductId,
                    WarehouseId = request.SourceWarehouseId,
                    MovementType = MovementType.TransferOut,
                    Quantity = -request.Quantity,
                    UnitCost = product.CurrentAverageCost,
                    ReferenceType = "Transfer",
                    ReferenceId = idempotencyOut,
                    Notes = $"Transfer to '{destWarehouse.Name}'. {request.Notes}".Trim(),
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = userId
                };

                // Create Inbound Movement
                var movementIn = new StockMovement
                {
                    ProductId = request.ProductId,
                    WarehouseId = request.DestinationWarehouseId,
                    MovementType = MovementType.TransferIn,
                    Quantity = request.Quantity,
                    UnitCost = product.CurrentAverageCost,
                    ReferenceType = "Transfer",
                    ReferenceId = request.IdempotencyToken + "-IN",
                    Notes = $"Transfer from '{sourceWarehouse.Name}'. {request.Notes}".Trim(),
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = userId
                };

                _context.StockMovements.AddRange(movementOut, movementIn);
                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return InventoryOperationResult.Ok(sourceStock.QuantityOnHand, movementOut.Id, movementIn.Id);
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return InventoryOperationResult.Fail($"Failed to execute transfer: {ex.Message}");
            }
        }

        public async Task<InventoryOperationResult> AdjustStockAsync(AdjustStockViewModel request, string? userId)
        {
            if (request.NewQuantityOnHand < 0)
                return InventoryOperationResult.Fail("Stock balance cannot be set to a negative number.");

            // Idempotency check
            if (!string.IsNullOrWhiteSpace(request.IdempotencyToken))
            {
                var existingMovement = await _context.StockMovements
                    .FirstOrDefaultAsync(m => m.ReferenceId == request.IdempotencyToken);
                if (existingMovement != null)
                {
                    return InventoryOperationResult.Ok(request.NewQuantityOnHand, existingMovement.Id);
                }
            }

            await using var transaction = (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                    return InventoryOperationResult.Fail("Product not found.");

                var warehouse = await _context.Warehouses.FindAsync(request.WarehouseId);
                if (warehouse == null)
                    return InventoryOperationResult.Fail("Warehouse not found.");

                var stockItem = await _context.StockItems
                    .FirstOrDefaultAsync(s => s.ProductId == request.ProductId && s.WarehouseId == request.WarehouseId);

                if (stockItem == null)
                {
                    stockItem = new StockItem
                    {
                        ProductId = request.ProductId,
                        WarehouseId = request.WarehouseId,
                        QuantityOnHand = 0,
                        QuantityReserved = 0
                    };
                    _context.StockItems.Add(stockItem);
                }

                decimal diff = request.NewQuantityOnHand - stockItem.QuantityOnHand;
                if (diff == 0)
                {
                    return InventoryOperationResult.Ok(stockItem.QuantityOnHand);
                }

                stockItem.QuantityOnHand = request.NewQuantityOnHand;

                var movement = new StockMovement
                {
                    ProductId = request.ProductId,
                    WarehouseId = request.WarehouseId,
                    MovementType = MovementType.StocktakingAdjustment,
                    Quantity = diff, // Can be positive or negative
                    UnitCost = product.CurrentAverageCost,
                    ReferenceType = "Adjustment",
                    ReferenceId = request.IdempotencyToken,
                    Notes = $"Reason: {request.Reason}".Trim(),
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = userId
                };

                _context.StockMovements.Add(movement);
                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return InventoryOperationResult.Ok(stockItem.QuantityOnHand, movement.Id);
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return InventoryOperationResult.Fail($"Failed to adjust stock: {ex.Message}");
            }
        }

        public async Task<decimal> GetProductStockInWarehouseAsync(int productId, int warehouseId)
        {
            var stockItem = await _context.StockItems
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);

            return stockItem?.QuantityOnHand ?? 0;
        }

        public async Task<decimal> GetProductTotalStockAsync(int productId)
        {
            var quantities = await _context.StockItems
                .Where(s => s.ProductId == productId)
                .Select(s => s.QuantityOnHand)
                .ToListAsync();
            return quantities.Sum();
        }

        public async Task<bool> VerifyStockIntegrityAsync(int productId, int warehouseId)
        {
            var stockItem = await _context.StockItems
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);

            var movements = await _context.StockMovements
                .Where(m => m.ProductId == productId && m.WarehouseId == warehouseId)
                .Select(m => m.Quantity)
                .ToListAsync();

            var currentOnHand = stockItem?.QuantityOnHand ?? 0;
            return currentOnHand == movements.Sum();
        }
    }
}
