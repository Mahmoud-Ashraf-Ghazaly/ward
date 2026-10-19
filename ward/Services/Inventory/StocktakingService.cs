using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using ward.Entities.Enums;
using ward.Entities.Inventory;
using ward.Models.Inventory;

namespace ward.Services.Inventory
{
    public class StocktakingService : IStocktakingService
    {
        private readonly ApplicationDbContext _context;

        public StocktakingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<StocktakingListItemViewModel>> GetAllSessionsAsync(int? warehouseId = null)
        {
            var query = _context.Stocktakings
                .Include(s => s.Warehouse)
                .Include(s => s.Lines)
                .AsNoTracking();

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                query = query.Where(s => s.WarehouseId == warehouseId.Value);
            }

            return await query
                .OrderByDescending(s => s.StartDateUtc)
                .ThenByDescending(s => s.Id)
                .Select(s => new StocktakingListItemViewModel
                {
                    Id = s.Id,
                    StocktakingNumber = s.StocktakingNumber,
                    WarehouseId = s.WarehouseId,
                    WarehouseName = s.Warehouse.Name,
                    StartDateUtc = s.StartDateUtc,
                    CompletedDateUtc = s.CompletedDateUtc,
                    ApprovedDateUtc = s.ApprovedDateUtc,
                    Status = s.Status,
                    LinesCount = s.Lines.Count,
                    TotalShortageQuantity = s.TotalShortageQuantity,
                    TotalSurplusQuantity = s.TotalSurplusQuantity,
                    NetDifferenceQuantity = s.NetDifferenceQuantity,
                    TotalVarianceCost = s.TotalVarianceCost,
                    Reason = s.Reason,
                    CreatedByUserId = s.CreatedByUserId,
                    ApprovedByUserId = s.ApprovedByUserId
                })
                .ToListAsync();
        }

        public async Task<StocktakingDetailsViewModel?> GetSessionDetailsAsync(int id)
        {
            var s = await _context.Stocktakings
                .Include(st => st.Warehouse)
                .Include(st => st.Lines)
                    .ThenInclude(l => l.Product)
                        .ThenInclude(p => p.UnitOfMeasure)
                .Include(st => st.Lines)
                    .ThenInclude(l => l.Product)
                        .ThenInclude(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(st => st.Id == id);

            if (s == null) return null;

            // Fetch live current stock items to indicate concurrent movement deltas
            var productIds = s.Lines.Select(l => l.ProductId).ToList();
            var liveStocks = await _context.StockItems
                .Where(si => si.WarehouseId == s.WarehouseId && productIds.Contains(si.ProductId))
                .ToDictionaryAsync(si => si.ProductId, si => si.QuantityOnHand);

            var lines = s.Lines.Select(l =>
            {
                var liveStock = liveStocks.TryGetValue(l.ProductId, out var cur) ? cur : 0;
                var currentSysQty = (s.Status == StocktakingStatus.Posted) ? l.CurrentSystemQuantity : liveStock;
                return new StocktakingLineInputDto
                {
                    LineId = l.Id,
                    ProductId = l.ProductId,
                    ProductName = l.Product.Name,
                    ProductSKU = l.Product.SKU,
                    UnitSymbol = l.Product.UnitOfMeasure?.Symbol ?? "pcs",
                    CategoryName = l.Product.Category?.Name ?? "General",
                    SystemQuantity = l.SystemQuantity,
                    CountedQuantity = l.CountedQuantity,
                    CurrentSystemQuantity = currentSysQty,
                    UnitCost = l.UnitCost,
                    Reason = l.Reason
                };
            }).OrderBy(l => l.ProductName).ToList();

            return new StocktakingDetailsViewModel
            {
                Id = s.Id,
                StocktakingNumber = s.StocktakingNumber,
                WarehouseId = s.WarehouseId,
                WarehouseName = s.Warehouse.Name,
                StartDateUtc = s.StartDateUtc,
                CompletedDateUtc = s.CompletedDateUtc,
                ApprovedDateUtc = s.ApprovedDateUtc,
                Status = s.Status,
                Reason = s.Reason,
                Notes = s.Notes,
                CreatedByUserId = s.CreatedByUserId,
                ApprovedByUserId = s.ApprovedByUserId,
                TotalShortageQuantity = s.TotalShortageQuantity,
                TotalSurplusQuantity = s.TotalSurplusQuantity,
                NetDifferenceQuantity = s.NetDifferenceQuantity,
                TotalVarianceCost = s.TotalVarianceCost,
                Lines = lines
            };
        }

        public async Task<StocktakingCountViewModel?> GetSessionForCountingAsync(int id)
        {
            var s = await _context.Stocktakings
                .Include(st => st.Warehouse)
                .Include(st => st.Lines)
                    .ThenInclude(l => l.Product)
                        .ThenInclude(p => p.UnitOfMeasure)
                .Include(st => st.Lines)
                    .ThenInclude(l => l.Product)
                        .ThenInclude(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(st => st.Id == id);

            if (s == null) return null;
            if (s.Status == StocktakingStatus.Posted || s.Status == StocktakingStatus.Cancelled) return null;

            var productIds = s.Lines.Select(l => l.ProductId).ToList();
            var liveStocks = await _context.StockItems
                .Where(si => si.WarehouseId == s.WarehouseId && productIds.Contains(si.ProductId))
                .ToDictionaryAsync(si => si.ProductId, si => si.QuantityOnHand);

            var lines = s.Lines.Select(l => new StocktakingLineInputDto
            {
                LineId = l.Id,
                ProductId = l.ProductId,
                ProductName = l.Product.Name,
                ProductSKU = l.Product.SKU,
                UnitSymbol = l.Product.UnitOfMeasure?.Symbol ?? "pcs",
                CategoryName = l.Product.Category?.Name ?? "General",
                SystemQuantity = l.SystemQuantity,
                CountedQuantity = l.CountedQuantity,
                CurrentSystemQuantity = liveStocks.TryGetValue(l.ProductId, out var cur) ? cur : 0,
                UnitCost = l.UnitCost,
                Reason = l.Reason
            }).OrderBy(l => l.ProductName).ToList();

            return new StocktakingCountViewModel
            {
                Id = s.Id,
                StocktakingNumber = s.StocktakingNumber,
                WarehouseId = s.WarehouseId,
                WarehouseName = s.Warehouse.Name,
                StartDateUtc = s.StartDateUtc,
                Status = s.Status,
                Reason = s.Reason,
                Notes = s.Notes,
                Lines = lines
            };
        }

        public async Task<StocktakingReviewViewModel?> GetSessionForReviewAsync(int id)
        {
            var s = await _context.Stocktakings
                .Include(st => st.Warehouse)
                .Include(st => st.Lines)
                    .ThenInclude(l => l.Product)
                        .ThenInclude(p => p.UnitOfMeasure)
                .Include(st => st.Lines)
                    .ThenInclude(l => l.Product)
                        .ThenInclude(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(st => st.Id == id);

            if (s == null) return null;
            if (s.Status == StocktakingStatus.Posted || s.Status == StocktakingStatus.Cancelled) return null;

            var productIds = s.Lines.Select(l => l.ProductId).ToList();
            var liveStocks = await _context.StockItems
                .Where(si => si.WarehouseId == s.WarehouseId && productIds.Contains(si.ProductId))
                .ToDictionaryAsync(si => si.ProductId, si => si.QuantityOnHand);

            var lines = s.Lines.Select(l => new StocktakingLineInputDto
            {
                LineId = l.Id,
                ProductId = l.ProductId,
                ProductName = l.Product.Name,
                ProductSKU = l.Product.SKU,
                UnitSymbol = l.Product.UnitOfMeasure?.Symbol ?? "pcs",
                CategoryName = l.Product.Category?.Name ?? "General",
                SystemQuantity = l.SystemQuantity,
                CountedQuantity = l.CountedQuantity,
                CurrentSystemQuantity = liveStocks.TryGetValue(l.ProductId, out var cur) ? cur : 0,
                UnitCost = l.UnitCost,
                Reason = l.Reason
            }).OrderBy(l => l.ProductName).ToList();

            var totalShortage = lines.Where(l => l.DifferenceQuantity < 0).Sum(l => Math.Abs(l.DifferenceQuantity));
            var totalSurplus = lines.Where(l => l.DifferenceQuantity > 0).Sum(l => l.DifferenceQuantity);
            var netDiff = lines.Sum(l => l.DifferenceQuantity);
            var totalVarianceCost = lines.Sum(l => l.VarianceCost);

            return new StocktakingReviewViewModel
            {
                Id = s.Id,
                StocktakingNumber = s.StocktakingNumber,
                WarehouseId = s.WarehouseId,
                WarehouseName = s.Warehouse.Name,
                StartDateUtc = s.StartDateUtc,
                Status = s.Status,
                Reason = s.Reason,
                Notes = s.Notes,
                CreatedByUserId = s.CreatedByUserId,
                TotalShortageQuantity = totalShortage,
                TotalSurplusQuantity = totalSurplus,
                NetDifferenceQuantity = netDiff,
                TotalVarianceCost = totalVarianceCost,
                Lines = lines
            };
        }

        public async Task<(bool Success, string? Error, int SessionId)> CreateSessionAsync(StocktakingCreateViewModel model, string? userId)
        {
            var warehouse = await _context.Warehouses.FindAsync(model.WarehouseId);
            if (warehouse == null || !warehouse.IsActive)
            {
                return (false, "Selected warehouse not found or is inactive.", 0);
            }

            var stocktakingNumber = await GenerateNextStocktakingNumberAsync();

            // Snapshot all active products and their current warehouse stock
            var products = await _context.Products
                .Where(p => p.IsActive)
                .ToListAsync();

            var productIds = products.Select(p => p.Id).ToList();
            var stockMap = await _context.StockItems
                .Where(s => s.WarehouseId == model.WarehouseId && productIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId, s => s.QuantityOnHand);

            var session = new Stocktaking
            {
                StocktakingNumber = stocktakingNumber,
                WarehouseId = model.WarehouseId,
                StartDateUtc = DateTime.UtcNow,
                Status = StocktakingStatus.Counting,
                Reason = model.Reason?.Trim(),
                Notes = model.Notes?.Trim(),
                CreatedByUserId = userId
            };

            foreach (var prod in products)
            {
                var systemQty = stockMap.TryGetValue(prod.Id, out var qty) ? qty : 0;
                session.Lines.Add(new StocktakingLine
                {
                    ProductId = prod.Id,
                    SystemQuantity = systemQty,
                    CountedQuantity = systemQty, // Default counted equals system snapshot initially
                    DifferenceQuantity = 0,
                    CurrentSystemQuantity = systemQty,
                    UnitCost = prod.CurrentAverageCost > 0 ? prod.CurrentAverageCost : prod.UnitPrice,
                    VarianceCost = 0
                });
            }

            _context.Stocktakings.Add(session);
            await _context.SaveChangesAsync();

            return (true, null, session.Id);
        }

        public async Task<(bool Success, string? Error)> SaveCountsAsync(StocktakingCountViewModel model, string? userId)
        {
            var session = await _context.Stocktakings
                .Include(s => s.Lines)
                .FirstOrDefaultAsync(s => s.Id == model.Id);

            if (session == null)
            {
                return (false, "Stocktaking session not found.");
            }

            if (session.Status == StocktakingStatus.Posted || session.Status == StocktakingStatus.Cancelled)
            {
                return (false, $"Cannot update counts because session status is '{session.Status}'.");
            }

            var lineMap = session.Lines.ToDictionary(l => l.Id);
            decimal totalShortage = 0;
            decimal totalSurplus = 0;
            decimal netDiff = 0;
            decimal totalVarianceCost = 0;

            foreach (var inputLine in model.Lines)
            {
                if (lineMap.TryGetValue(inputLine.LineId, out var line))
                {
                    line.CountedQuantity = Math.Max(0, inputLine.CountedQuantity);
                    line.DifferenceQuantity = line.CountedQuantity - line.SystemQuantity;
                    line.VarianceCost = Math.Round(line.DifferenceQuantity * line.UnitCost, 2);
                    line.Reason = inputLine.Reason?.Trim();

                    if (line.DifferenceQuantity < 0) totalShortage += Math.Abs(line.DifferenceQuantity);
                    else if (line.DifferenceQuantity > 0) totalSurplus += line.DifferenceQuantity;
                    netDiff += line.DifferenceQuantity;
                    totalVarianceCost += line.VarianceCost;
                }
            }

            session.TotalShortageQuantity = totalShortage;
            session.TotalSurplusQuantity = totalSurplus;
            session.NetDifferenceQuantity = netDiff;
            session.TotalVarianceCost = totalVarianceCost;
            session.Status = StocktakingStatus.Reviewed; // Marked ready for approval review
            session.Notes = model.Notes?.Trim();

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> ApproveAndPostAdjustmentsAsync(int id, string? approvalNotes, string? idempotencyToken, string? userId)
        {
            // 8. Idempotency guard: prevent duplicate approvals and repeated adjustments
            if (!string.IsNullOrWhiteSpace(idempotencyToken))
            {
                var existingApproved = await _context.Stocktakings
                    .FirstOrDefaultAsync(s => s.IdempotencyToken == idempotencyToken && s.Status == StocktakingStatus.Posted);

                if (existingApproved != null)
                {
                    return (true, null);
                }
            }

            var session = await _context.Stocktakings
                .Include(s => s.Lines)
                    .ThenInclude(l => l.Product)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null)
            {
                return (false, "Stocktaking session not found.");
            }

            if (session.Status == StocktakingStatus.Posted)
            {
                return (false, "This stocktaking session has already been approved and posted.");
            }

            if (session.Status == StocktakingStatus.Cancelled)
            {
                return (false, "Cannot approve a cancelled stocktaking session.");
            }

            await using var transaction = (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var productIds = session.Lines.Select(l => l.ProductId).ToList();
                var stockItems = await _context.StockItems
                    .Where(si => si.WarehouseId == session.WarehouseId && productIds.Contains(si.ProductId))
                    .ToDictionaryAsync(si => si.ProductId);

                decimal totalShortage = 0;
                decimal totalSurplus = 0;
                decimal netDiff = 0;
                decimal totalVarianceCost = 0;

                foreach (var line in session.Lines)
                {
                    if (!stockItems.TryGetValue(line.ProductId, out var stockItem))
                    {
                        stockItem = new StockItem
                        {
                            ProductId = line.ProductId,
                            WarehouseId = session.WarehouseId,
                            QuantityOnHand = 0
                        };
                        _context.StockItems.Add(stockItem);
                        stockItems[line.ProductId] = stockItem;
                    }

                    // 9. Handle stock changes that occur while a count is in progress
                    var currentStock = stockItem.QuantityOnHand;
                    line.CurrentSystemQuantity = currentStock;

                    // Physical variance detected during count: Counted - Snapshot
                    var physicalVariance = line.CountedQuantity - line.SystemQuantity;
                    line.DifferenceQuantity = physicalVariance;
                    line.VarianceCost = Math.Round(physicalVariance * line.UnitCost, 2);

                    if (physicalVariance < 0) totalShortage += Math.Abs(physicalVariance);
                    else if (physicalVariance > 0) totalSurplus += physicalVariance;
                    netDiff += physicalVariance;
                    totalVarianceCost += line.VarianceCost;

                    if (physicalVariance != 0)
                    {
                        // Apply the variance delta to current live inventory:
                        // newStock = currentStock + physicalVariance
                        var targetStock = currentStock + physicalVariance;
                        if (targetStock < 0)
                        {
                            if (transaction != null) await transaction.RollbackAsync();
                            return (false, $"Adjustment would cause negative inventory for '{line.Product.Name}'. Current: {currentStock:G29}, Physical Variance: {physicalVariance:G29}.");
                        }

                        stockItem.QuantityOnHand = targetStock;

                        // 7. Record approved adjustment in the stock movement ledger
                        var concurrentDelta = currentStock - line.SystemQuantity;
                        var concurrentNote = Math.Abs(concurrentDelta) > 0.0001m
                            ? $" [In-progress concurrent movement: {concurrentDelta:+0.####;-0.####;0}]"
                            : "";

                        _context.StockMovements.Add(new StockMovement
                        {
                            ProductId = line.ProductId,
                            WarehouseId = session.WarehouseId,
                            MovementType = MovementType.StocktakingAdjustment,
                            Quantity = physicalVariance, // positive for surplus, negative for shortage
                            UnitCost = line.UnitCost,
                            ReferenceType = "Stocktaking",
                            ReferenceId = session.StocktakingNumber,
                            Notes = string.IsNullOrWhiteSpace(line.Reason)
                                ? $"Stocktaking adjustment {session.StocktakingNumber} (Count: {line.CountedQuantity:G29}, Snapshot: {line.SystemQuantity:G29}, AtApproval: {currentStock:G29}){concurrentNote}"
                                : $"Stocktaking adjustment ({line.Reason}) (Count: {line.CountedQuantity:G29}, Snapshot: {line.SystemQuantity:G29}, AtApproval: {currentStock:G29}){concurrentNote}",
                            CreatedAtUtc = DateTime.UtcNow,
                            CreatedByUserId = userId
                        });
                    }
                }

                session.TotalShortageQuantity = totalShortage;
                session.TotalSurplusQuantity = totalSurplus;
                session.NetDifferenceQuantity = netDiff;
                session.TotalVarianceCost = totalVarianceCost;
                session.Status = StocktakingStatus.Posted;
                session.ApprovedDateUtc = DateTime.UtcNow;
                session.CompletedDateUtc = DateTime.UtcNow;
                session.ApprovedByUserId = userId;
                session.IdempotencyToken = idempotencyToken;

                if (!string.IsNullOrWhiteSpace(approvalNotes))
                {
                    session.Notes = string.IsNullOrWhiteSpace(session.Notes)
                        ? $"Approval: {approvalNotes.Trim()}"
                        : $"{session.Notes} | Approval: {approvalNotes.Trim()}";
                }

                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync();
                return (false, $"Approval failed: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? Error)> CancelSessionAsync(int id, string? userId)
        {
            var session = await _context.Stocktakings.FindAsync(id);
            if (session == null) return (false, "Session not found.");
            if (session.Status == StocktakingStatus.Posted) return (false, "Cannot cancel an already posted session.");

            session.Status = StocktakingStatus.Cancelled;
            session.CompletedDateUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<string> GenerateNextStocktakingNumberAsync()
        {
            var prefix = $"STK-{DateTime.UtcNow:yyyyMM}-";
            var count = await _context.Stocktakings
                .CountAsync(s => s.StocktakingNumber.StartsWith(prefix));
            return $"{prefix}{(count + 1):D4}";
        }
    }
}
