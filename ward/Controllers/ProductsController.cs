using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ward.Constants;
using ward.Data;
using ward.Entities.Enums;
using ward.Models.Catalog;
using ward.Services.Catalog;

namespace ward.Controllers
{
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public class ProductsController : Controller
    {
        private readonly IProductService _productService;
        private readonly ApplicationDbContext _context;

        public ProductsController(IProductService productService, ApplicationDbContext context)
        {
            _productService = productService;
            _context = context;
        }

        [AllowAnonymous] // Allow viewing catalog for authenticated staff or anonymous read
        public async Task<IActionResult> Index(ProductListFilterViewModel filter)
        {
            var result = await _productService.GetPagedProductsAsync(filter);
            ViewBag.Categories = new SelectList(await _context.Categories.OrderBy(c => c.Name).ToListAsync(), "Id", "Name", filter.CategoryId);
            return View(result);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _productService.GetDetailsByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateDropDownsAsync();
            return View(new ProductCreateEditViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductCreateEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropDownsAsync(model.CategoryId, model.UnitOfMeasureId);
                return View(model);
            }

            var (success, error, productId) = await _productService.CreateAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to create product.");
                await PopulateDropDownsAsync(model.CategoryId, model.UnitOfMeasureId);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Product '{model.Name}' created successfully with SKU: {model.SKU.ToUpperInvariant()}.";
            return RedirectToAction(nameof(Details), new { id = productId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var model = await _productService.GetForEditAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            await PopulateDropDownsAsync(model.CategoryId, model.UnitOfMeasureId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductCreateEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropDownsAsync(model.CategoryId, model.UnitOfMeasureId);
                return View(model);
            }

            var (success, error) = await _productService.UpdateAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to update product.");
                await PopulateDropDownsAsync(model.CategoryId, model.UnitOfMeasureId);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Product '{model.Name}' updated successfully.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var (success, error) = await _productService.ToggleActiveAsync(id);
            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to update product status.";
            }
            else
            {
                TempData["SuccessMessage"] = "Product status updated successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        [AllowAnonymous]
        public async Task<IActionResult> PrintBarcode(int id, int count = 1)
        {
            var label = await _productService.GetBarcodeLabelAsync(id, count);
            if (label == null)
            {
                return NotFound();
            }

            return View(label);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Lookup(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { message = "Query cannot be empty." });
            }

            var product = await _productService.LookupBySkuOrBarcodeAsync(query);
            if (product == null)
            {
                return NotFound(new { message = $"No active product matching '{query}' was found." });
            }

            return Ok(product);
        }

        private async Task PopulateDropDownsAsync(int? selectedCategoryId = null, int? selectedUomId = null)
        {
            ViewBag.Categories = new SelectList(
                await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", selectedCategoryId);

            ViewBag.UnitsOfMeasure = new SelectList(
                await _context.UnitsOfMeasure.OrderBy(u => u.Name).ToListAsync(),
                "Id", "Name", selectedUomId);
        }
    }
}
