using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using ward.Entities.Enums;
using ward.Models.Reports;

namespace ward.Services.Reports
{
    public class ReportingService : IReportingService
    {
        private readonly ApplicationDbContext _context;

        public ReportingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<SalesReportViewModel> GetSalesReportAsync(DateTime? fromDate, DateTime? toDate, string grouping = "Daily")
        {
            var invoiceQuery = _context.Invoices
                .Include(i => i.Items)
                .Where(i => i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled)
                .AsNoTracking();

            var returnQuery = _context.InvoiceReturns
                .Where(r => r.Status == ReturnStatus.Completed)
                .AsNoTracking();

            if (fromDate.HasValue)
            {
                var fromUtc = fromDate.Value.Date.ToUniversalTime();
                invoiceQuery = invoiceQuery.Where(i => i.InvoiceDateUtc >= fromUtc);
                returnQuery = returnQuery.Where(r => r.ReturnDateUtc >= fromUtc);
            }

            if (toDate.HasValue)
            {
                var toUtc = toDate.Value.Date.AddDays(1).ToUniversalTime();
                invoiceQuery = invoiceQuery.Where(i => i.InvoiceDateUtc < toUtc);
                returnQuery = returnQuery.Where(r => r.ReturnDateUtc < toUtc);
            }

            var invoices = await invoiceQuery.ToListAsync();
            var returns = await returnQuery.ToListAsync();

            var model = new SalesReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                GroupingPeriod = grouping,
                TotalInvoicesCount = invoices.Count,
                TotalGrossSales = invoices.Sum(i => i.SubTotal),
                TotalDiscounts = invoices.Sum(i => i.DiscountAmount),
                TotalTax = invoices.Sum(i => i.TaxAmount),
                TotalGrandTotal = invoices.Sum(i => i.GrandTotal),
                TotalPaid = invoices.Sum(i => i.PaidAmount),
                TotalRefunds = returns.Sum(r => r.TotalRefundAmount),
                TotalCostOfGoodsSold = invoices.SelectMany(i => i.Items).Sum(it => it.Quantity * it.UnitCost)
            };

            // Grouping
            var isMonthly = string.Equals(grouping, "Monthly", StringComparison.OrdinalIgnoreCase);

            var invoiceGroups = invoices
                .GroupBy(i => isMonthly ? i.InvoiceDateUtc.ToString("yyyy-MM") : i.InvoiceDateUtc.ToString("yyyy-MM-dd"))
                .ToDictionary(g => g.Key, g => g.ToList());

            var returnGroups = returns
                .GroupBy(r => isMonthly ? r.ReturnDateUtc.ToString("yyyy-MM") : r.ReturnDateUtc.ToString("yyyy-MM-dd"))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalRefundAmount));

            var allKeys = invoiceGroups.Keys.Union(returnGroups.Keys).OrderByDescending(k => k).ToList();

            foreach (var key in allKeys)
            {
                var invList = invoiceGroups.TryGetValue(key, out var list) ? list : new List<Entities.Sales.Invoice>();
                var refAmount = returnGroups.TryGetValue(key, out var refVal) ? refVal : 0;

                var subTotal = invList.Sum(i => i.SubTotal);
                var disc = invList.Sum(i => i.DiscountAmount);
                var tax = invList.Sum(i => i.TaxAmount);
                var grand = invList.Sum(i => i.GrandTotal);
                var cost = invList.SelectMany(i => i.Items).Sum(it => it.Quantity * it.UnitCost);

                model.Rows.Add(new SalesPeriodRowDto
                {
                    PeriodLabel = key,
                    InvoicesCount = invList.Count,
                    SubTotal = subTotal,
                    DiscountAmount = disc,
                    TaxAmount = tax,
                    GrandTotal = grand,
                    RefundedAmount = refAmount,
                    TotalCost = cost
                });
            }

            return model;
        }

        public async Task<PurchasesReturnsReportViewModel> GetPurchasesReturnsReportAsync(DateTime? fromDate, DateTime? toDate, int? supplierId, int? warehouseId)
        {
            var invoiceQuery = _context.PurchaseInvoices
                .Include(pi => pi.Supplier)
                .Include(pi => pi.Warehouse)
                .Where(pi => pi.Status != PurchaseInvoiceStatus.Draft && pi.Status != PurchaseInvoiceStatus.Cancelled)
                .AsNoTracking();

            var returnQuery = _context.PurchaseReturns
                .Include(pr => pr.Supplier)
                .Include(pr => pr.PurchaseInvoice)
                .Where(pr => pr.Status == PurchaseReturnStatus.Completed)
                .AsNoTracking();

            if (fromDate.HasValue)
            {
                var fromUtc = fromDate.Value.Date.ToUniversalTime();
                invoiceQuery = invoiceQuery.Where(pi => pi.InvoiceDateUtc >= fromUtc);
                returnQuery = returnQuery.Where(pr => pr.ReturnDateUtc >= fromUtc);
            }

            if (toDate.HasValue)
            {
                var toUtc = toDate.Value.Date.AddDays(1).ToUniversalTime();
                invoiceQuery = invoiceQuery.Where(pi => pi.InvoiceDateUtc < toUtc);
                returnQuery = returnQuery.Where(pr => pr.ReturnDateUtc < toUtc);
            }

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                invoiceQuery = invoiceQuery.Where(pi => pi.SupplierId == supplierId.Value);
                returnQuery = returnQuery.Where(pr => pr.SupplierId == supplierId.Value);
            }

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                invoiceQuery = invoiceQuery.Where(pi => pi.WarehouseId == warehouseId.Value);
            }

            var invoices = await invoiceQuery.OrderByDescending(pi => pi.InvoiceDateUtc).ToListAsync();
            var returns = await returnQuery.OrderByDescending(pr => pr.ReturnDateUtc).ToListAsync();

            return new PurchasesReturnsReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                SupplierId = supplierId,
                WarehouseId = warehouseId,
                TotalPurchaseInvoices = invoices.Count,
                TotalPurchasesAmount = invoices.Sum(pi => pi.GrandTotal),
                TotalPaidToSuppliers = invoices.Sum(pi => pi.PaidAmount),
                TotalPendingSupplierBalance = invoices.Sum(pi => pi.GrandTotal - pi.PaidAmount),
                TotalPurchaseReturnsCount = returns.Count,
                TotalPurchaseReturnAmount = returns.Sum(pr => pr.TotalRefundAmount),
                Invoices = invoices.Select(pi => new PurchaseReportRowDto
                {
                    Id = pi.Id,
                    InvoiceNumber = pi.InvoiceNumber,
                    InvoiceDateUtc = pi.InvoiceDateUtc,
                    SupplierName = pi.Supplier.Name,
                    WarehouseName = pi.Warehouse.Name,
                    GrandTotal = pi.GrandTotal,
                    PaidAmount = pi.PaidAmount,
                    Status = pi.Status
                }).ToList(),
                Returns = returns.Select(pr => new PurchaseReturnReportRowDto
                {
                    Id = pr.Id,
                    ReturnNumber = pr.ReturnNumber,
                    ReturnDateUtc = pr.ReturnDateUtc,
                    SupplierName = pr.Supplier.Name,
                    OriginalInvoiceNumber = pr.PurchaseInvoice?.InvoiceNumber ?? "",
                    TotalRefundAmount = pr.TotalRefundAmount,
                    Status = pr.Status
                }).ToList()
            };
        }

        public async Task<ProductCategorySalesReportViewModel> GetProductCategorySalesReportAsync(DateTime? fromDate, DateTime? toDate, int? categoryId)
        {
            var itemsQuery = _context.InvoiceItems
                .Include(it => it.Invoice)
                .Include(it => it.Product)
                    .ThenInclude(p => p.Category)
                .Include(it => it.Product)
                    .ThenInclude(p => p.UnitOfMeasure)
                .Where(it => it.Invoice.Status != InvoiceStatus.Draft && it.Invoice.Status != InvoiceStatus.Cancelled)
                .AsNoTracking();

            if (fromDate.HasValue)
            {
                var fromUtc = fromDate.Value.Date.ToUniversalTime();
                itemsQuery = itemsQuery.Where(it => it.Invoice.InvoiceDateUtc >= fromUtc);
            }

            if (toDate.HasValue)
            {
                var toUtc = toDate.Value.Date.AddDays(1).ToUniversalTime();
                itemsQuery = itemsQuery.Where(it => it.Invoice.InvoiceDateUtc < toUtc);
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                itemsQuery = itemsQuery.Where(it => it.Product.CategoryId == categoryId.Value);
            }

            var items = await itemsQuery.ToListAsync();

            var productGroups = items
                .GroupBy(it => new { it.ProductId, it.Product.Name, it.Product.SKU, CategoryName = it.Product.Category?.Name ?? "General", UnitSymbol = it.Product.UnitOfMeasure?.Symbol ?? "pcs" })
                .Select(g => new ProductSalesSummaryDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Name,
                    ProductSKU = g.Key.SKU,
                    CategoryName = g.Key.CategoryName,
                    UnitSymbol = g.Key.UnitSymbol,
                    QuantitySold = g.Sum(i => i.Quantity),
                    TotalRevenue = g.Sum(i => i.LineTotal),
                    TotalCost = g.Sum(i => i.Quantity * i.UnitCost)
                })
                .OrderByDescending(p => p.TotalRevenue)
                .ToList();

            var categoryGroups = items
                .GroupBy(it => it.Product.Category?.Name ?? "General")
                .Select(g => new CategorySalesSummaryDto
                {
                    CategoryName = g.Key,
                    ItemsCount = g.Select(i => i.ProductId).Distinct().Count(),
                    TotalQuantitySold = g.Sum(i => i.Quantity),
                    TotalRevenue = g.Sum(i => i.LineTotal),
                    TotalCost = g.Sum(i => i.Quantity * i.UnitCost)
                })
                .OrderByDescending(c => c.TotalRevenue)
                .ToList();

            return new ProductCategorySalesReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                CategoryId = categoryId,
                TotalItemsSold = items.Sum(i => i.Quantity),
                TotalRevenue = items.Sum(i => i.LineTotal),
                TotalCostOfGoodsSold = items.Sum(i => i.Quantity * i.UnitCost),
                CategoryBreakdown = categoryGroups,
                ProductBreakdown = productGroups
            };
        }

        public async Task<WarehouseStockReportViewModel> GetWarehouseStockReportAsync(int? warehouseId, int? categoryId, string? search)
        {
            var query = _context.StockItems
                .Include(s => s.Warehouse)
                .Include(s => s.Product)
                    .ThenInclude(p => p.Category)
                .Include(s => s.Product)
                    .ThenInclude(p => p.UnitOfMeasure)
                .Where(s => s.Product.IsActive && s.Warehouse.IsActive)
                .AsNoTracking();

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                query = query.Where(s => s.WarehouseId == warehouseId.Value);
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(s => s.Product.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(s => s.Product.Name.ToLower().Contains(term) || s.Product.SKU.ToLower().Contains(term));
            }

            var stockItems = await query.OrderBy(s => s.Warehouse.Name).ThenBy(s => s.Product.Name).ToListAsync();

            var rows = stockItems.Select(s => new WarehouseStockRowDto
            {
                WarehouseName = s.Warehouse.Name,
                ProductId = s.ProductId,
                ProductName = s.Product.Name,
                ProductSKU = s.Product.SKU,
                CategoryName = s.Product.Category?.Name ?? "General",
                UnitSymbol = s.Product.UnitOfMeasure?.Symbol ?? "pcs",
                QuantityOnHand = s.QuantityOnHand,
                UnitCost = s.Product.CurrentAverageCost,
                MinimumStockLevel = s.Product.MinimumStockLevel
            }).ToList();

            return new WarehouseStockReportViewModel
            {
                WarehouseId = warehouseId,
                CategoryId = categoryId,
                SearchQuery = search,
                TotalProductsTracked = rows.Count,
                TotalUnitsInStock = rows.Sum(r => r.QuantityOnHand),
                TotalInventoryValuation = rows.Sum(r => r.TotalValuation),
                Rows = rows
            };
        }

        public async Task<StockMovementsReportViewModel> GetStockMovementsReportAsync(DateTime? fromDate, DateTime? toDate, int? warehouseId, int? productId, MovementType? movementType)
        {
            var query = _context.StockMovements
                .Include(m => m.Warehouse)
                .Include(m => m.Product)
                .AsNoTracking();

            if (fromDate.HasValue)
            {
                var fromUtc = fromDate.Value.Date.ToUniversalTime();
                query = query.Where(m => m.CreatedAtUtc >= fromUtc);
            }

            if (toDate.HasValue)
            {
                var toUtc = toDate.Value.Date.AddDays(1).ToUniversalTime();
                query = query.Where(m => m.CreatedAtUtc < toUtc);
            }

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                query = query.Where(m => m.WarehouseId == warehouseId.Value);
            }

            if (productId.HasValue && productId.Value > 0)
            {
                query = query.Where(m => m.ProductId == productId.Value);
            }

            if (movementType.HasValue)
            {
                query = query.Where(m => m.MovementType == movementType.Value);
            }

            var movements = await query.OrderByDescending(m => m.CreatedAtUtc).Take(500).ToListAsync();

            var inQty = movements.Where(m => m.Quantity > 0).Sum(m => m.Quantity);
            var outQty = movements.Where(m => m.Quantity < 0).Sum(m => Math.Abs(m.Quantity));

            return new StockMovementsReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                WarehouseId = warehouseId,
                ProductId = productId,
                MovementType = movementType,
                TotalMovementsCount = movements.Count,
                TotalInQuantity = inQty,
                TotalOutQuantity = outQty,
                Movements = movements.Select(m => new StockMovementRowDto
                {
                    Id = m.Id,
                    CreatedAtUtc = m.CreatedAtUtc,
                    WarehouseName = m.Warehouse?.Name ?? "Warehouse",
                    ProductName = m.Product?.Name ?? "Product",
                    ProductSKU = m.Product?.SKU ?? "",
                    MovementType = m.MovementType,
                    Quantity = m.Quantity,
                    UnitCost = m.UnitCost,
                    ReferenceType = m.ReferenceType,
                    ReferenceId = m.ReferenceId,
                    Notes = m.Notes,
                    CreatedByUserId = m.CreatedByUserId
                }).ToList()
            };
        }

        public async Task<LowStockReportViewModel> GetLowStockReportAsync(int? warehouseId)
        {
            var query = _context.StockItems
                .Include(s => s.Warehouse)
                .Include(s => s.Product)
                    .ThenInclude(p => p.Category)
                .Include(s => s.Product)
                    .ThenInclude(p => p.UnitOfMeasure)
                .Where(s => s.Product.IsActive && s.Warehouse.IsActive && s.QuantityOnHand <= s.Product.MinimumStockLevel)
                .AsNoTracking();

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                query = query.Where(s => s.WarehouseId == warehouseId.Value);
            }

            var items = await query.ToListAsync();
            var orderedItems = items.OrderBy(s => s.QuantityOnHand).ToList();

            var rows = orderedItems.Select(s => new LowStockRowDto
            {
                ProductId = s.ProductId,
                ProductName = s.Product.Name,
                ProductSKU = s.Product.SKU,
                CategoryName = s.Product.Category?.Name ?? "General",
                WarehouseName = s.Warehouse.Name,
                UnitSymbol = s.Product.UnitOfMeasure?.Symbol ?? "pcs",
                QuantityOnHand = s.QuantityOnHand,
                MinimumStockLevel = s.Product.MinimumStockLevel
            }).ToList();

            return new LowStockReportViewModel
            {
                WarehouseId = warehouseId,
                OutOfStockCount = rows.Count(r => r.IsOutOfStock),
                LowStockCount = rows.Count(r => !r.IsOutOfStock),
                Rows = rows
            };
        }

        public async Task<StocktakingVariancesReportViewModel> GetStocktakingVariancesReportAsync(DateTime? fromDate, DateTime? toDate, int? warehouseId)
        {
            var query = _context.Stocktakings
                .Include(st => st.Warehouse)
                .Include(st => st.Lines)
                .Where(st => st.Status == StocktakingStatus.Posted)
                .AsNoTracking();

            if (fromDate.HasValue)
            {
                var fromUtc = fromDate.Value.Date.ToUniversalTime();
                query = query.Where(st => st.ApprovedDateUtc >= fromUtc);
            }

            if (toDate.HasValue)
            {
                var toUtc = toDate.Value.Date.AddDays(1).ToUniversalTime();
                query = query.Where(st => st.ApprovedDateUtc < toUtc);
            }

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                query = query.Where(st => st.WarehouseId == warehouseId.Value);
            }

            var list = await query.OrderByDescending(st => st.ApprovedDateUtc).ToListAsync();

            return new StocktakingVariancesReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                WarehouseId = warehouseId,
                TotalSessionsCount = list.Count,
                TotalShortageQuantity = list.Sum(st => st.TotalShortageQuantity),
                TotalSurplusQuantity = list.Sum(st => st.TotalSurplusQuantity),
                NetQuantityVariance = list.Sum(st => st.NetDifferenceQuantity),
                TotalVarianceCostImpact = list.Sum(st => st.TotalVarianceCost),
                Rows = list.Select(st => new StocktakingVarianceRowDto
                {
                    Id = st.Id,
                    StocktakingNumber = st.StocktakingNumber,
                    WarehouseName = st.Warehouse.Name,
                    ApprovedDateUtc = st.ApprovedDateUtc,
                    ApprovedByUserId = st.ApprovedByUserId,
                    TotalShortageQuantity = st.TotalShortageQuantity,
                    TotalSurplusQuantity = st.TotalSurplusQuantity,
                    NetDifferenceQuantity = st.NetDifferenceQuantity,
                    TotalVarianceCost = st.TotalVarianceCost,
                    Reason = st.Reason,
                    ItemsCount = st.Lines.Count
                }).ToList()
            };
        }

        public async Task<BouquetConsumptionReportViewModel> GetBouquetConsumptionReportAsync(DateTime? fromDate, DateTime? toDate, int? bouquetProductId)
        {
            var query = _context.InvoiceItemComponentConsumptions
                .Include(cc => cc.ComponentProduct)
                    .ThenInclude(p => p.UnitOfMeasure)
                .Include(cc => cc.InvoiceItem)
                    .ThenInclude(ii => ii.Invoice)
                .Where(cc => cc.InvoiceItem.Invoice.Status != InvoiceStatus.Draft && cc.InvoiceItem.Invoice.Status != InvoiceStatus.Cancelled)
                .AsNoTracking();

            if (fromDate.HasValue)
            {
                var fromUtc = fromDate.Value.Date.ToUniversalTime();
                query = query.Where(cc => cc.InvoiceItem.Invoice.InvoiceDateUtc >= fromUtc);
            }

            if (toDate.HasValue)
            {
                var toUtc = toDate.Value.Date.AddDays(1).ToUniversalTime();
                query = query.Where(cc => cc.InvoiceItem.Invoice.InvoiceDateUtc < toUtc);
            }

            if (bouquetProductId.HasValue && bouquetProductId.Value > 0)
            {
                query = query.Where(cc => cc.InvoiceItem.ProductId == bouquetProductId.Value);
            }

            var consumptions = await query.ToListAsync();

            var grouped = consumptions
                .GroupBy(cc => new { cc.ComponentProductId, cc.ComponentProduct.Name, cc.ComponentProduct.SKU, UnitSymbol = cc.ComponentProduct.UnitOfMeasure?.Symbol ?? "pcs", cc.UnitCost })
                .Select(g => new BouquetComponentUsageDto
                {
                    ComponentProductId = g.Key.ComponentProductId,
                    ComponentProductName = g.Key.Name,
                    ComponentSKU = g.Key.SKU,
                    UnitSymbol = g.Key.UnitSymbol,
                    TotalQuantityConsumed = g.Sum(c => c.QuantityConsumed),
                    UnitCost = g.Key.UnitCost,
                    BouquetsProducedCount = g.Select(c => c.InvoiceItemId).Distinct().Count()
                })
                .OrderByDescending(b => b.TotalCost)
                .ToList();

            var totalBouquets = consumptions.Select(c => c.InvoiceItemId).Distinct().Count();

            return new BouquetConsumptionReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                BouquetProductId = bouquetProductId,
                TotalBouquetUnitsSold = totalBouquets,
                DistinctComponentsCount = grouped.Count,
                TotalComponentCost = grouped.Sum(g => g.TotalCost),
                Rows = grouped
            };
        }

        public async Task<CustomerSupplierBalancesReportViewModel> GetCustomerSupplierBalancesReportAsync()
        {
            var customers = await _context.Customers
                .Include(c => c.Invoices)
                .Where(c => c.IsActive)
                .AsNoTracking()
                .ToListAsync();

            var suppliers = await _context.Suppliers
                .Include(s => s.Invoices)
                .Where(s => s.IsActive)
                .AsNoTracking()
                .ToListAsync();

            var customerRows = customers.Select(c => new CustomerBalanceRowDto
            {
                CustomerId = c.Id,
                CustomerName = c.Name,
                Phone = c.Phone,
                Email = c.Email,
                TotalInvoiced = c.Invoices.Where(i => i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled).Sum(i => i.GrandTotal),
                TotalPaid = c.Invoices.Where(i => i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled).Sum(i => i.PaidAmount)
            }).ToList();

            var supplierRows = suppliers.Select(s => new SupplierBalanceRowDto
            {
                SupplierId = s.Id,
                SupplierName = s.Name,
                ContactPerson = s.ContactPerson,
                Phone = s.Phone,
                CurrentBalance = s.CurrentBalance,
                TotalInvoiced = s.Invoices.Where(i => i.Status != PurchaseInvoiceStatus.Draft && i.Status != PurchaseInvoiceStatus.Cancelled).Sum(i => i.GrandTotal),
                TotalPaid = s.Invoices.Where(i => i.Status != PurchaseInvoiceStatus.Draft && i.Status != PurchaseInvoiceStatus.Cancelled).Sum(i => i.PaidAmount)
            }).ToList();

            return new CustomerSupplierBalancesReportViewModel
            {
                TotalCustomerReceivables = customerRows.Sum(c => c.OutstandingBalance),
                TotalSupplierPayables = supplierRows.Sum(s => s.CurrentBalance),
                Customers = customerRows,
                Suppliers = supplierRows
            };
        }
    }
}
