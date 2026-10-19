using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ward.Constants;
using ward.Data;
using ward.Models.Inventory;
using ward.Services.Inventory;

namespace ward.Controllers
{
    [Authorize(Policy = AppPolicies.ManageInventory)]
    public class InventoryController : Controller
    {
        private readonly IInventoryService _inventoryService;
        private readonly ApplicationDbContext _context;

        public InventoryController(IInventoryService inventoryService, ApplicationDbContext context)
        {
            _inventoryService = inventoryService;
            _context = context;
        }

        public async Task<IActionResult> Index(WarehouseStockFilterViewModel filter)
        {
            var result = await _inventoryService.GetWarehouseStockPagedAsync(filter);
            ViewBag.Warehouses = new SelectList(await _context.Warehouses.Where(w => w.IsActive).OrderBy(w => w.Name).ToListAsync(), "Id", "Name", filter.WarehouseId);
            ViewBag.Categories = new SelectList(await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(), "Id", "Name", filter.CategoryId);
            return View(result);
        }

        public async Task<IActionResult> Movements(StockMovementFilterViewModel filter)
        {
            var result = await _inventoryService.GetStockMovementsPagedAsync(filter);
            ViewBag.Warehouses = new SelectList(await _context.Warehouses.OrderBy(w => w.Name).ToListAsync(), "Id", "Name", filter.WarehouseId);
            ViewBag.Products = new SelectList(await _context.Products.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", filter.ProductId);
            return View(result);
        }

        public async Task<IActionResult> Receive(int? productId = null, int? warehouseId = null)
        {
            await PopulateDropdownsAsync(productId, warehouseId);
            var model = new ReceiveStockViewModel();
            if (productId.HasValue) model.ProductId = productId.Value;
            if (warehouseId.HasValue) model.WarehouseId = warehouseId.Value;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(ReceiveStockViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model.ProductId, model.WarehouseId);
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "InventoryClerk";
            var result = await _inventoryService.ReceiveStockAsync(model, userId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to receive stock.");
                await PopulateDropdownsAsync(model.ProductId, model.WarehouseId);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Stock receipt posted successfully. New quantity on hand: {result.NewQuantityOnHand:0.####}.";
            return RedirectToAction(nameof(Index), new { warehouseId = model.WarehouseId });
        }

        public async Task<IActionResult> Issue(int? productId = null, int? warehouseId = null)
        {
            await PopulateDropdownsAsync(productId, warehouseId);
            var model = new IssueStockViewModel();
            if (productId.HasValue) model.ProductId = productId.Value;
            if (warehouseId.HasValue) model.WarehouseId = warehouseId.Value;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Issue(IssueStockViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model.ProductId, model.WarehouseId);
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "InventoryClerk";
            var result = await _inventoryService.IssueStockAsync(model, userId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to issue stock.");
                await PopulateDropdownsAsync(model.ProductId, model.WarehouseId);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Stock issue posted successfully. Remaining quantity on hand: {result.NewQuantityOnHand:0.####}.";
            return RedirectToAction(nameof(Index), new { warehouseId = model.WarehouseId });
        }

        public async Task<IActionResult> Transfer(int? productId = null, int? sourceWarehouseId = null)
        {
            await PopulateDropdownsAsync(productId, sourceWarehouseId);
            var model = new TransferStockViewModel();
            if (productId.HasValue) model.ProductId = productId.Value;
            if (sourceWarehouseId.HasValue) model.SourceWarehouseId = sourceWarehouseId.Value;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Transfer(TransferStockViewModel model)
        {
            if (model.SourceWarehouseId == model.DestinationWarehouseId)
            {
                ModelState.AddModelError(nameof(model.DestinationWarehouseId), "Destination warehouse must be different from source warehouse.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model.ProductId, model.SourceWarehouseId);
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "InventoryClerk";
            var result = await _inventoryService.TransferStockAsync(model, userId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to transfer stock.");
                await PopulateDropdownsAsync(model.ProductId, model.SourceWarehouseId);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Inter-warehouse transfer completed successfully. Source balance: {result.NewQuantityOnHand:0.####}.";
            return RedirectToAction(nameof(Index), new { warehouseId = model.DestinationWarehouseId });
        }

        public async Task<IActionResult> Adjust(int? productId = null, int? warehouseId = null)
        {
            await PopulateDropdownsAsync(productId, warehouseId);
            var model = new AdjustStockViewModel();
            if (productId.HasValue) model.ProductId = productId.Value;
            if (warehouseId.HasValue)
            {
                model.WarehouseId = warehouseId.Value;
                if (productId.HasValue)
                {
                    model.NewQuantityOnHand = await _inventoryService.GetProductStockInWarehouseAsync(productId.Value, warehouseId.Value);
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(AdjustStockViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model.ProductId, model.WarehouseId);
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "InventoryClerk";
            var result = await _inventoryService.AdjustStockAsync(model, userId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to adjust stock.");
                await PopulateDropdownsAsync(model.ProductId, model.WarehouseId);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Stock balance adjusted successfully to {result.NewQuantityOnHand:0.####}.";
            return RedirectToAction(nameof(Index), new { warehouseId = model.WarehouseId });
        }

        [HttpGet]
        public async Task<IActionResult> GetProductStock(int productId, int warehouseId)
        {
            var stock = await _inventoryService.GetProductStockInWarehouseAsync(productId, warehouseId);
            return Ok(new { available = stock });
        }

        private async Task PopulateDropdownsAsync(int? selectedProductId = null, int? selectedWarehouseId = null)
        {
            ViewBag.Products = new SelectList(
                await _context.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "Id", "Name", selectedProductId);

            ViewBag.Warehouses = new SelectList(
                await _context.Warehouses.Where(w => w.IsActive).OrderBy(w => w.Name).ToListAsync(),
                "Id", "Name", selectedWarehouseId);
        }
    }
}
