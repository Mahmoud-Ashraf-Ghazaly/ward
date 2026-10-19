using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using ward.Entities.Catalog;
using ward.Entities.Enums;
using ward.Entities.Inventory;
using ward.Entities.Purchasing;
using ward.Models.Purchasing;

namespace ward.Services.Purchasing
{
    public class PurchaseService : IPurchaseService
    {
        private readonly ApplicationDbContext _context;

        public PurchaseService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PurchaseInvoiceFilterViewModel> GetInvoicesPagedAsync(PurchaseInvoiceFilterViewModel filter)
        {
            var query = _context.PurchaseInvoices
                .Include(p => p.Supplier)
                .Include(p => p.Warehouse)
                .Include(p => p.Items)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
            {
                var q = filter.SearchQuery.Trim().ToLower();
                query = query.Where(p =>
                    p.InvoiceNumber.ToLower().Contains(q) ||
                    (p.SupplierInvoiceNumber != null && p.SupplierInvoiceNumber.ToLower().Contains(q)) ||
                    p.Supplier.Name.ToLower().Contains(q) ||
                    (p.Notes != null && p.Notes.ToLower().Contains(q)));
            }

            if (filter.SupplierId.HasValue && filter.SupplierId.Value > 0)
            {
                query = query.Where(p => p.SupplierId == filter.SupplierId.Value);
            }

            if (filter.WarehouseId.HasValue && filter.WarehouseId.Value > 0)
            {
                query = query.Where(p => p.WarehouseId == filter.WarehouseId.Value);
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(p => p.Status == filter.Status.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(p => p.InvoiceDateUtc >= filter.FromDate.Value.ToUniversalTime());
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(p => p.InvoiceDateUtc <= filter.ToDate.Value.ToUniversalTime().AddDays(1));
            }

            filter.TotalCount = await query.CountAsync();

            var page = Math.Max(1, filter.PageNumber);
            var pageSize = filter.PageSize > 0 ? filter.PageSize : 15;

            filter.Invoices = await query
                .OrderByDescending(p => p.InvoiceDateUtc)
                .ThenByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PurchaseInvoiceListItemViewModel
                {
                    Id = p.Id,
                    InvoiceNumber = p.InvoiceNumber,
                    SupplierInvoiceNumber = p.SupplierInvoiceNumber,
                    SupplierId = p.SupplierId,
                    SupplierName = p.Supplier.Name,
                    WarehouseId = p.WarehouseId,
                    WarehouseName = p.Warehouse.Name,
                    InvoiceDateUtc = p.InvoiceDateUtc,
                    DueDateUtc = p.DueDateUtc,
                    Status = p.Status,
                    SubTotal = p.SubTotal,
                    DiscountAmount = p.DiscountAmount,
                    TaxAmount = p.TaxAmount,
                    GrandTotal = p.GrandTotal,
                    PaidAmount = p.PaidAmount,
                    ItemsCount = p.Items.Count,
                    CreatedAtUtc = p.CreatedAtUtc
                })
                .ToListAsync();

            return filter;
        }

        public async Task<PurchaseInvoiceDetailsViewModel?> GetInvoiceDetailsAsync(int id)
        {
            var invoice = await _context.PurchaseInvoices
                .Include(p => p.Supplier)
                .Include(p => p.Warehouse)
                .Include(p => p.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(pr => pr.UnitOfMeasure)
                .Include(p => p.Returns)
                    .ThenInclude(r => r.Items)
                .Include(p => p.Payments)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (invoice == null) return null;

            return new PurchaseInvoiceDetailsViewModel
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                SupplierInvoiceNumber = invoice.SupplierInvoiceNumber,
                SupplierId = invoice.SupplierId,
                SupplierName = invoice.Supplier.Name,
                SupplierContactPerson = invoice.Supplier.ContactPerson,
                SupplierPhone = invoice.Supplier.Phone,
                SupplierEmail = invoice.Supplier.Email,
                SupplierTaxNumber = invoice.Supplier.TaxNumber,
                SupplierAddress = invoice.Supplier.Address,
                SupplierCurrentBalance = invoice.Supplier.CurrentBalance,
                WarehouseId = invoice.WarehouseId,
                WarehouseName = invoice.Warehouse.Name,
                InvoiceDateUtc = invoice.InvoiceDateUtc,
                DueDateUtc = invoice.DueDateUtc,
                Status = invoice.Status,
                SubTotal = invoice.SubTotal,
                DiscountAmount = invoice.DiscountAmount,
                TaxRatePercentage = invoice.TaxRatePercentage,
                TaxAmount = invoice.TaxAmount,
                GrandTotal = invoice.GrandTotal,
                PaidAmount = invoice.PaidAmount,
                Notes = invoice.Notes,
                PostedAtUtc = invoice.PostedAtUtc,
                PostedByUserId = invoice.PostedByUserId,
                CreatedAtUtc = invoice.CreatedAtUtc,
                Items = invoice.Items.Select(i => new PurchaseInvoiceItemDetailsViewModel
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    ProductSKU = i.Product.SKU,
                    UnitSymbol = i.Product.UnitOfMeasure?.Symbol ?? "pcs",
                    Quantity = i.Quantity,
                    UnitCost = i.UnitCost,
                    DiscountAmount = i.DiscountAmount,
                    LineTotal = i.LineTotal,
                    ReturnedQuantity = i.ReturnedQuantity
                }).ToList(),
                Returns = invoice.Returns.Select(r => new PurchaseReturnListItemViewModel
                {
                    Id = r.Id,
                    ReturnNumber = r.ReturnNumber,
                    PurchaseInvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    SupplierName = invoice.Supplier.Name,
                    WarehouseName = invoice.Warehouse.Name,
                    ReturnDateUtc = r.ReturnDateUtc,
                    Status = r.Status,
                    Reason = r.Reason,
                    TotalRefundAmount = r.TotalRefundAmount,
                    ItemsCount = r.Items.Count
                }).ToList(),
                Payments = invoice.Payments.Select(pm => new SupplierPaymentListItemViewModel
                {
                    Id = pm.Id,
                    SupplierId = pm.SupplierId,
                    SupplierName = invoice.Supplier.Name,
                    PurchaseInvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    PaymentDateUtc = pm.PaymentDateUtc,
                    Amount = pm.Amount,
                    PaymentMethod = pm.PaymentMethod,
                    ReferenceNumber = pm.ReferenceNumber,
                    Notes = pm.Notes,
                    CreatedByUserId = pm.CreatedByUserId
                }).ToList()
            };
        }

        public async Task<PurchaseInvoiceCreateViewModel> PrepareCreateInvoiceViewModelAsync()
        {
            var nextInvoiceNumber = await GenerateNextInvoiceNumberAsync();
            return new PurchaseInvoiceCreateViewModel
            {
                InvoiceNumber = nextInvoiceNumber,
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                TaxRatePercentage = 0,
                Items = new List<PurchaseInvoiceItemFormViewModel>()
            };
        }

        public async Task<(bool Success, string? Error, int InvoiceId)> CreateInvoiceAsync(PurchaseInvoiceCreateViewModel model, string? userId)
        {
            if (model.SupplierId <= 0)
                return (false, "Please select a valid supplier.", 0);

            if (model.WarehouseId <= 0)
                return (false, "Please select a valid destination warehouse.", 0);

            if (model.Items == null || model.Items.Count == 0)
                return (false, "Purchase invoice must have at least one line item.", 0);

            var supplier = await _context.Suppliers.FindAsync(model.SupplierId);
            if (supplier == null || !supplier.IsActive)
                return (false, "Selected supplier not found or is inactive.", 0);

            var warehouse = await _context.Warehouses.FindAsync(model.WarehouseId);
            if (warehouse == null || !warehouse.IsActive)
                return (false, "Selected warehouse not found or is inactive.", 0);

            var duplicateNumber = await _context.PurchaseInvoices
                .AnyAsync(p => p.InvoiceNumber == model.InvoiceNumber.Trim());
            if (duplicateNumber)
                return (false, $"Purchase invoice number '{model.InvoiceNumber}' is already in use.", 0);

            // Validate and recalculate lines on the server
            var productIds = model.Items.Select(i => i.ProductId).Distinct().ToList();
            var productsInDb = await _context.Products
                .Include(p => p.UnitOfMeasure)
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            decimal subTotal = 0;
            var validatedItems = new List<PurchaseInvoiceItem>();

            foreach (var item in model.Items)
            {
                if (!productsInDb.TryGetValue(item.ProductId, out var product))
                    return (false, $"Product ID {item.ProductId} not found.", 0);

                if (!product.IsActive)
                    return (false, $"Product '{product.Name}' is inactive.", 0);

                if (product.ProductType == ProductType.Service)
                    return (false, $"Service product '{product.Name}' cannot be purchased into physical stock.", 0);

                if (item.Quantity <= 0)
                    return (false, $"Quantity for '{product.Name}' must be greater than zero.", 0);

                if (item.UnitCost < 0)
                    return (false, $"Unit cost for '{product.Name}' cannot be negative.", 0);

                if (item.DiscountAmount < 0)
                    item.DiscountAmount = 0;

                var lineTotal = Math.Max(0, (item.Quantity * item.UnitCost) - item.DiscountAmount);
                subTotal += lineTotal;

                validatedItems.Add(new PurchaseInvoiceItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost,
                    DiscountAmount = item.DiscountAmount,
                    LineTotal = lineTotal,
                    ReturnedQuantity = 0
                });
            }

            var discount = Math.Max(0, model.DiscountAmount);
            var taxableSubtotal = Math.Max(0, subTotal - discount);
            var taxRate = Math.Clamp(model.TaxRatePercentage, 0m, 100m);
            var taxAmount = Math.Round(taxableSubtotal * (taxRate / 100m), 2);
            var grandTotal = Math.Round(taxableSubtotal + taxAmount, 2);

            var invoice = new PurchaseInvoice
            {
                InvoiceNumber = model.InvoiceNumber.Trim(),
                SupplierInvoiceNumber = model.SupplierInvoiceNumber?.Trim(),
                SupplierId = model.SupplierId,
                WarehouseId = model.WarehouseId,
                InvoiceDateUtc = model.InvoiceDate.ToUniversalTime(),
                DueDateUtc = model.DueDate?.ToUniversalTime(),
                Status = PurchaseInvoiceStatus.Draft,
                SubTotal = Math.Round(subTotal, 2),
                DiscountAmount = discount,
                TaxRatePercentage = taxRate,
                TaxAmount = taxAmount,
                GrandTotal = grandTotal,
                PaidAmount = 0,
                Notes = model.Notes?.Trim(),
                IdempotencyToken = model.IdempotencyToken,
                CreatedAtUtc = DateTime.UtcNow,
                Items = validatedItems
            };

            _context.PurchaseInvoices.Add(invoice);
            await _context.SaveChangesAsync();

            if (model.PostImmediately)
            {
                var postResult = await PostInvoiceAsync(invoice.Id, userId, model.IdempotencyToken);
                if (!postResult.Success)
                {
                    return (true, $"Invoice created as Draft, but posting failed: {postResult.Error}", invoice.Id);
                }
            }

            return (true, null, invoice.Id);
        }

        public async Task<(bool Success, string? Error)> PostInvoiceAsync(int invoiceId, string? userId, string? idempotencyToken = null)
        {
            var invoice = await _context.PurchaseInvoices
                .Include(p => p.Supplier)
                .Include(p => p.Warehouse)
                .Include(p => p.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(p => p.Id == invoiceId);

            if (invoice == null)
                return (false, "Purchase invoice not found.");

            if (invoice.Status != PurchaseInvoiceStatus.Draft)
            {
                // Idempotency: If already posted and matching token, return success
                if (invoice.Status == PurchaseInvoiceStatus.Posted && !string.IsNullOrWhiteSpace(idempotencyToken) && invoice.IdempotencyToken == idempotencyToken)
                {
                    return (true, null);
                }
                return (false, $"Cannot post invoice in status '{invoice.Status}'. Only Draft invoices can be posted.");
            }

            // Begin atomic transaction
            await using var transaction = (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                // 1. For each item: Increase warehouse stock and update weighted average cost
                foreach (var item in invoice.Items)
                {
                    var stockItem = await _context.StockItems
                        .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.WarehouseId == invoice.WarehouseId);

                    if (stockItem == null)
                    {
                        stockItem = new StockItem
                        {
                            ProductId = item.ProductId,
                            WarehouseId = invoice.WarehouseId,
                            QuantityOnHand = 0,
                            QuantityReserved = 0
                        };
                        _context.StockItems.Add(stockItem);
                    }

                    // Weighted average cost update across all warehouses
                    var allStockList = await _context.StockItems
                        .Where(s => s.ProductId == item.ProductId)
                        .Select(s => s.QuantityOnHand)
                        .ToListAsync();
                    var totalStock = allStockList.Sum();

                    if (totalStock + item.Quantity > 0)
                    {
                        var totalVal = (totalStock * item.Product.CurrentAverageCost) + (item.Quantity * item.UnitCost);
                        item.Product.CurrentAverageCost = Math.Round(totalVal / (totalStock + item.Quantity), 2);
                    }
                    else
                    {
                        item.Product.CurrentAverageCost = item.UnitCost;
                    }

                    stockItem.QuantityOnHand += item.Quantity;

                    // Log StockMovement
                    var movement = new StockMovement
                    {
                        ProductId = item.ProductId,
                        WarehouseId = invoice.WarehouseId,
                        MovementType = MovementType.PurchaseIn,
                        Quantity = item.Quantity,
                        UnitCost = item.UnitCost,
                        ReferenceType = "PurchaseInvoice",
                        ReferenceId = invoice.InvoiceNumber,
                        Notes = $"Purchased from {invoice.Supplier.Name}. Ext Ref: {invoice.SupplierInvoiceNumber}".Trim(),
                        CreatedAtUtc = DateTime.UtcNow,
                        CreatedByUserId = userId
                    };
                    _context.StockMovements.Add(movement);
                }

                // 2. Increase supplier balance (amount owed to supplier)
                invoice.Supplier.CurrentBalance += invoice.GrandTotal;

                // 3. Mark invoice as Posted
                invoice.Status = PurchaseInvoiceStatus.Posted;
                invoice.PostedAtUtc = DateTime.UtcNow;
                invoice.PostedByUserId = userId;

                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return (false, $"Failed to post purchase invoice: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? Error)> CancelInvoiceAsync(int invoiceId, string? userId)
        {
            var invoice = await _context.PurchaseInvoices.FindAsync(invoiceId);
            if (invoice == null)
                return (false, "Invoice not found.");

            if (invoice.Status != PurchaseInvoiceStatus.Draft)
                return (false, "Only Draft purchase invoices can be cancelled. Posted invoices must be reversed via Purchase Returns.");

            invoice.Status = PurchaseInvoiceStatus.Cancelled;
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<PurchaseReturnCreateViewModel?> PrepareCreateReturnViewModelAsync(int invoiceId)
        {
            var invoice = await _context.PurchaseInvoices
                .Include(p => p.Supplier)
                .Include(p => p.Warehouse)
                .Include(p => p.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(pr => pr.UnitOfMeasure)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == invoiceId);

            if (invoice == null || (invoice.Status != PurchaseInvoiceStatus.Posted && invoice.Status != PurchaseInvoiceStatus.PartiallyReturned))
            {
                return null;
            }

            var nextReturnNumber = await GenerateNextReturnNumberAsync();

            return new PurchaseReturnCreateViewModel
            {
                PurchaseInvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                SupplierId = invoice.SupplierId,
                SupplierName = invoice.Supplier.Name,
                WarehouseId = invoice.WarehouseId,
                WarehouseName = invoice.Warehouse.Name,
                ReturnNumber = nextReturnNumber,
                ReturnDate = DateTime.Today,
                Items = invoice.Items.Select(i => new PurchaseReturnItemFormViewModel
                {
                    PurchaseInvoiceItemId = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    ProductSKU = i.Product.SKU,
                    UnitSymbol = i.Product.UnitOfMeasure?.Symbol ?? "pcs",
                    PurchasedQuantity = i.Quantity,
                    AlreadyReturnedQuantity = i.ReturnedQuantity,
                    EligibleQuantity = Math.Max(0, i.Quantity - i.ReturnedQuantity),
                    ReturnQuantity = 0,
                    UnitCost = i.UnitCost,
                    Disposition = ReturnDisposition.Restocked
                }).ToList()
            };
        }

        public async Task<(bool Success, string? Error, int ReturnId)> CreateAndProcessReturnAsync(PurchaseReturnCreateViewModel model, string? userId)
        {
            // Idempotency guard
            if (!string.IsNullOrWhiteSpace(model.IdempotencyToken))
            {
                var existing = await _context.PurchaseReturns
                    .FirstOrDefaultAsync(r => r.IdempotencyToken == model.IdempotencyToken);
                if (existing != null)
                {
                    return (true, null, existing.Id);
                }
            }

            var invoice = await _context.PurchaseInvoices
                .Include(p => p.Supplier)
                .Include(p => p.Warehouse)
                .Include(p => p.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(p => p.Id == model.PurchaseInvoiceId);

            if (invoice == null)
                return (false, "Target purchase invoice was not found.", 0);

            if (invoice.Status != PurchaseInvoiceStatus.Posted && invoice.Status != PurchaseInvoiceStatus.PartiallyReturned)
                return (false, "Returns can only be issued against Posted or Partially Returned purchase invoices.", 0);

            var itemsToReturn = model.Items.Where(i => i.ReturnQuantity > 0).ToList();
            if (itemsToReturn.Count == 0)
                return (false, "Please specify a return quantity greater than zero for at least one item.", 0);

            // Server-side validation: return quantity <= eligible quantity & warehouse has stock
            var invoiceItemsMap = invoice.Items.ToDictionary(i => i.Id);
            var productIds = itemsToReturn.Select(i => i.ProductId).Distinct().ToList();

            var stockItemsMap = await _context.StockItems
                .Where(s => s.WarehouseId == invoice.WarehouseId && productIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId);

            foreach (var item in itemsToReturn)
            {
                if (!invoiceItemsMap.TryGetValue(item.PurchaseInvoiceItemId, out var invItem))
                    return (false, $"Invoice line item for product ID {item.ProductId} not found.", 0);

                var eligibleQty = invItem.Quantity - invItem.ReturnedQuantity;
                if (item.ReturnQuantity > eligibleQty)
                {
                    return (false, $"Return quantity for '{invItem.Product.Name}' ({item.ReturnQuantity:G29}) exceeds eligible purchased quantity ({eligibleQty:G29}).", 0);
                }

                stockItemsMap.TryGetValue(item.ProductId, out var stockItem);
                var availableStock = stockItem?.QuantityOnHand ?? 0;
                if (availableStock < item.ReturnQuantity)
                {
                    return (false, $"Insufficient stock in warehouse '{invoice.Warehouse.Name}' to return '{invItem.Product.Name}'. Available: {availableStock:G29}, Return requested: {item.ReturnQuantity:G29}.", 0);
                }
            }

            // Begin atomic transaction
            await using var transaction = (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var returnNumber = string.IsNullOrWhiteSpace(model.ReturnNumber)
                    ? await GenerateNextReturnNumberAsync()
                    : model.ReturnNumber.Trim();

                var returnRecord = new PurchaseReturn
                {
                    ReturnNumber = returnNumber,
                    PurchaseInvoiceId = invoice.Id,
                    SupplierId = invoice.SupplierId,
                    WarehouseId = invoice.WarehouseId,
                    ReturnDateUtc = model.ReturnDate.ToUniversalTime(),
                    Status = PurchaseReturnStatus.Completed,
                    Reason = model.Reason.Trim(),
                    Notes = model.Notes?.Trim(),
                    IdempotencyToken = model.IdempotencyToken,
                    CompletedAtUtc = DateTime.UtcNow,
                    CompletedByUserId = userId,
                    CreatedAtUtc = DateTime.UtcNow
                };

                decimal totalRefund = 0;

                foreach (var item in itemsToReturn)
                {
                    var invItem = invoiceItemsMap[item.PurchaseInvoiceItemId];
                    var stockItem = stockItemsMap[item.ProductId];

                    // 1. Deduct stock from warehouse
                    stockItem.QuantityOnHand -= item.ReturnQuantity;

                    // 2. Increase returned quantity on invoice item
                    invItem.ReturnedQuantity += item.ReturnQuantity;

                    var lineRefund = Math.Round(item.ReturnQuantity * invItem.UnitCost, 2);
                    totalRefund += lineRefund;

                    returnRecord.Items.Add(new PurchaseReturnItem
                    {
                        PurchaseInvoiceItemId = invItem.Id,
                        ProductId = invItem.ProductId,
                        Quantity = item.ReturnQuantity,
                        UnitCost = invItem.UnitCost,
                        LineRefundTotal = lineRefund,
                        Disposition = item.Disposition
                    });

                    // 3. Log stock movement
                    var movementType = item.Disposition == ReturnDisposition.Discarded
                        ? MovementType.DamagedDiscardOut
                        : MovementType.PurchaseReturnOut;

                    var movement = new StockMovement
                    {
                        ProductId = invItem.ProductId,
                        WarehouseId = invoice.WarehouseId,
                        MovementType = movementType,
                        Quantity = -item.ReturnQuantity,
                        UnitCost = invItem.UnitCost,
                        ReferenceType = "PurchaseReturn",
                        ReferenceId = returnRecord.ReturnNumber,
                        Notes = $"Purchase Return for invoice {invoice.InvoiceNumber}. Reason: {model.Reason}".Trim(),
                        CreatedAtUtc = DateTime.UtcNow,
                        CreatedByUserId = userId
                    };
                    _context.StockMovements.Add(movement);
                }

                returnRecord.TotalRefundAmount = totalRefund;
                _context.PurchaseReturns.Add(returnRecord);

                // 4. Adjust supplier balance (decrease balance owed to supplier)
                invoice.Supplier.CurrentBalance = Math.Max(0, invoice.Supplier.CurrentBalance - totalRefund);

                // 5. Update invoice status (PartiallyReturned vs FullyReturned)
                var totalPurchased = invoice.Items.Sum(i => i.Quantity);
                var totalReturned = invoice.Items.Sum(i => i.ReturnedQuantity);

                if (totalReturned >= totalPurchased)
                {
                    invoice.Status = PurchaseInvoiceStatus.FullyReturned;
                }
                else
                {
                    invoice.Status = PurchaseInvoiceStatus.PartiallyReturned;
                }

                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return (true, null, returnRecord.Id);
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return (false, $"Failed to process purchase return: {ex.Message}", 0);
            }
        }

        public async Task<PurchaseReturnDetailsViewModel?> GetReturnDetailsAsync(int id)
        {
            var ret = await _context.PurchaseReturns
                .Include(r => r.PurchaseInvoice)
                .Include(r => r.Supplier)
                .Include(r => r.Warehouse)
                .Include(r => r.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(pr => pr.UnitOfMeasure)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (ret == null) return null;

            return new PurchaseReturnDetailsViewModel
            {
                Id = ret.Id,
                ReturnNumber = ret.ReturnNumber,
                PurchaseInvoiceId = ret.PurchaseInvoiceId,
                InvoiceNumber = ret.PurchaseInvoice.InvoiceNumber,
                SupplierId = ret.SupplierId,
                SupplierName = ret.Supplier.Name,
                WarehouseId = ret.WarehouseId,
                WarehouseName = ret.Warehouse.Name,
                ReturnDateUtc = ret.ReturnDateUtc,
                Status = ret.Status,
                Reason = ret.Reason,
                TotalRefundAmount = ret.TotalRefundAmount,
                Notes = ret.Notes,
                CompletedAtUtc = ret.CompletedAtUtc,
                CompletedByUserId = ret.CompletedByUserId,
                CreatedAtUtc = ret.CreatedAtUtc,
                Items = ret.Items.Select(i => new PurchaseReturnItemDetailsViewModel
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    ProductSKU = i.Product.SKU,
                    UnitSymbol = i.Product.UnitOfMeasure?.Symbol ?? "pcs",
                    Quantity = i.Quantity,
                    UnitCost = i.UnitCost,
                    LineRefundTotal = i.LineRefundTotal,
                    Disposition = i.Disposition
                }).ToList()
            };
        }

        public async Task<List<PurchaseReturnListItemViewModel>> GetAllReturnsAsync()
        {
            return await _context.PurchaseReturns
                .Include(r => r.PurchaseInvoice)
                .Include(r => r.Supplier)
                .Include(r => r.Warehouse)
                .Include(r => r.Items)
                .AsNoTracking()
                .OrderByDescending(r => r.ReturnDateUtc)
                .Select(r => new PurchaseReturnListItemViewModel
                {
                    Id = r.Id,
                    ReturnNumber = r.ReturnNumber,
                    PurchaseInvoiceId = r.PurchaseInvoiceId,
                    InvoiceNumber = r.PurchaseInvoice.InvoiceNumber,
                    SupplierName = r.Supplier.Name,
                    WarehouseName = r.Warehouse.Name,
                    ReturnDateUtc = r.ReturnDateUtc,
                    Status = r.Status,
                    Reason = r.Reason,
                    TotalRefundAmount = r.TotalRefundAmount,
                    ItemsCount = r.Items.Count
                })
                .ToListAsync();
        }

        public async Task<List<SupplierListItemViewModel>> GetAllSuppliersAsync()
        {
            return await _context.Suppliers
                .Include(s => s.Invoices)
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .Select(s => new SupplierListItemViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    ContactPerson = s.ContactPerson,
                    Phone = s.Phone,
                    Email = s.Email,
                    CurrentBalance = s.CurrentBalance,
                    InvoicesCount = s.Invoices.Count,
                    IsActive = s.IsActive
                })
                .ToListAsync();
        }

        public async Task<SupplierFormViewModel?> GetSupplierByIdAsync(int id)
        {
            var s = await _context.Suppliers.FindAsync(id);
            if (s == null) return null;

            return new SupplierFormViewModel
            {
                Id = s.Id,
                Name = s.Name,
                ContactPerson = s.ContactPerson,
                Phone = s.Phone,
                Email = s.Email,
                TaxNumber = s.TaxNumber,
                Address = s.Address,
                IsActive = s.IsActive,
                CurrentBalance = s.CurrentBalance
            };
        }

        public async Task<(bool Success, string? Error, int SupplierId)> SaveSupplierAsync(SupplierFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                return (false, "Supplier name is required.", 0);

            Supplier? supplier;
            if (model.Id > 0)
            {
                supplier = await _context.Suppliers.FindAsync(model.Id);
                if (supplier == null) return (false, "Supplier not found.", 0);
            }
            else
            {
                supplier = new Supplier { CreatedAtUtc = DateTime.UtcNow };
                _context.Suppliers.Add(supplier);
            }

            supplier.Name = model.Name.Trim();
            supplier.ContactPerson = model.ContactPerson?.Trim();
            supplier.Phone = model.Phone?.Trim();
            supplier.Email = model.Email?.Trim();
            supplier.TaxNumber = model.TaxNumber?.Trim();
            supplier.Address = model.Address?.Trim();
            supplier.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            return (true, null, supplier.Id);
        }

        public async Task<(bool Success, string? Error)> RecordSupplierPaymentAsync(SupplierPaymentFormViewModel model, string? userId)
        {
            if (model.Amount <= 0)
                return (false, "Payment amount must be greater than zero.");

            var supplier = await _context.Suppliers.FindAsync(model.SupplierId);
            if (supplier == null)
                return (false, "Supplier not found.");

            PurchaseInvoice? invoice = null;
            if (model.PurchaseInvoiceId.HasValue && model.PurchaseInvoiceId.Value > 0)
            {
                invoice = await _context.PurchaseInvoices.FindAsync(model.PurchaseInvoiceId.Value);
                if (invoice == null)
                    return (false, "Associated purchase invoice not found.");
            }

            await using var transaction = (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var payment = new SupplierPayment
                {
                    SupplierId = model.SupplierId,
                    PurchaseInvoiceId = invoice?.Id,
                    PaymentDateUtc = model.PaymentDate.ToUniversalTime(),
                    Amount = model.Amount,
                    PaymentMethod = model.PaymentMethod,
                    ReferenceNumber = model.ReferenceNumber?.Trim(),
                    Notes = model.Notes?.Trim(),
                    CreatedByUserId = userId,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _context.SupplierPayments.Add(payment);

                // Reduce what we owe the supplier
                supplier.CurrentBalance = Math.Max(0, supplier.CurrentBalance - model.Amount);

                if (invoice != null)
                {
                    invoice.PaidAmount += model.Amount;
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
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                return (false, $"Failed to record payment: {ex.Message}");
            }
        }

        public async Task<string> GenerateNextInvoiceNumberAsync()
        {
            var prefix = $"PINV-{DateTime.UtcNow:yyyyMM}-";
            var count = await _context.PurchaseInvoices
                .CountAsync(p => p.InvoiceNumber.StartsWith(prefix));
            return $"{prefix}{(count + 1):D4}";
        }

        public async Task<string> GenerateNextReturnNumberAsync()
        {
            var prefix = $"PRET-{DateTime.UtcNow:yyyyMM}-";
            var count = await _context.PurchaseReturns
                .CountAsync(r => r.ReturnNumber.StartsWith(prefix));
            return $"{prefix}{(count + 1):D4}";
        }
    }
}
