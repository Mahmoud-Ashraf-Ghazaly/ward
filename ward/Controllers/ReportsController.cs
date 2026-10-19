using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ward.Constants;
using ward.Data;
using ward.Entities.Enums;
using ward.Services.Reports;

namespace ward.Controllers
{
    [Authorize(Policy = AppPolicies.ViewAuditAndReports)]
    public class ReportsController : Controller
    {
        private readonly IReportingService _reportingService;
        private readonly ApplicationDbContext _context;

        public ReportsController(IReportingService reportingService, ApplicationDbContext context)
        {
            _reportingService = reportingService;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Sales(DateTime? fromDate, DateTime? toDate, string grouping = "Daily")
        {
            var model = await _reportingService.GetSalesReportAsync(fromDate, toDate, grouping);
            return View(model);
        }

        public async Task<IActionResult> Purchases(DateTime? fromDate, DateTime? toDate, int? supplierId, int? warehouseId)
        {
            var model = await _reportingService.GetPurchasesReturnsReportAsync(fromDate, toDate, supplierId, warehouseId);
            ViewBag.Suppliers = new SelectList(await _context.Suppliers.OrderBy(s => s.Name).ToListAsync(), "Id", "Name", supplierId);
            ViewBag.Warehouses = new SelectList(await _context.Warehouses.OrderBy(w => w.Name).ToListAsync(), "Id", "Name", warehouseId);
            return View(model);
        }

        public async Task<IActionResult> ProductSales(DateTime? fromDate, DateTime? toDate, int? categoryId)
        {
            var model = await _reportingService.GetProductCategorySalesReportAsync(fromDate, toDate, categoryId);
            ViewBag.Categories = new SelectList(await _context.Categories.OrderBy(c => c.Name).ToListAsync(), "Id", "Name", categoryId);
            return View(model);
        }

        public async Task<IActionResult> WarehouseStock(int? warehouseId, int? categoryId, string? search)
        {
            var model = await _reportingService.GetWarehouseStockReportAsync(warehouseId, categoryId, search);
            ViewBag.Warehouses = new SelectList(await _context.Warehouses.OrderBy(w => w.Name).ToListAsync(), "Id", "Name", warehouseId);
            ViewBag.Categories = new SelectList(await _context.Categories.OrderBy(c => c.Name).ToListAsync(), "Id", "Name", categoryId);
            return View(model);
        }

        public async Task<IActionResult> Movements(DateTime? fromDate, DateTime? toDate, int? warehouseId, int? productId, MovementType? movementType)
        {
            var model = await _reportingService.GetStockMovementsReportAsync(fromDate, toDate, warehouseId, productId, movementType);
            ViewBag.Warehouses = new SelectList(await _context.Warehouses.OrderBy(w => w.Name).ToListAsync(), "Id", "Name", warehouseId);
            ViewBag.Products = new SelectList(await _context.Products.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", productId);
            return View(model);
        }

        public async Task<IActionResult> LowStock(int? warehouseId)
        {
            var model = await _reportingService.GetLowStockReportAsync(warehouseId);
            ViewBag.Warehouses = new SelectList(await _context.Warehouses.OrderBy(w => w.Name).ToListAsync(), "Id", "Name", warehouseId);
            return View(model);
        }

        public async Task<IActionResult> StocktakingVariances(DateTime? fromDate, DateTime? toDate, int? warehouseId)
        {
            var model = await _reportingService.GetStocktakingVariancesReportAsync(fromDate, toDate, warehouseId);
            ViewBag.Warehouses = new SelectList(await _context.Warehouses.OrderBy(w => w.Name).ToListAsync(), "Id", "Name", warehouseId);
            return View(model);
        }

        public async Task<IActionResult> BouquetConsumption(DateTime? fromDate, DateTime? toDate, int? bouquetProductId)
        {
            var model = await _reportingService.GetBouquetConsumptionReportAsync(fromDate, toDate, bouquetProductId);
            ViewBag.Bouquets = new SelectList(await _context.Products.Where(p => p.ProductType == ProductType.Bouquet).OrderBy(p => p.Name).ToListAsync(), "Id", "Name", bouquetProductId);
            return View(model);
        }

        public async Task<IActionResult> Balances()
        {
            var model = await _reportingService.GetCustomerSupplierBalancesReportAsync();
            return View(model);
        }
    }
}
