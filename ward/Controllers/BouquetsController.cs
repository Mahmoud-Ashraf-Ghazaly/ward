using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ward.Constants;
using ward.Models.Production;
using ward.Services.Inventory;
using ward.Services.Production;

namespace ward.Controllers
{
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public class BouquetsController : Controller
    {
        private readonly IBouquetService _bouquetService;
        private readonly IWarehouseService _warehouseService;

        public BouquetsController(IBouquetService bouquetService, IWarehouseService warehouseService)
        {
            _bouquetService = bouquetService;
            _warehouseService = warehouseService;
        }

        public async Task<IActionResult> Index()
        {
            var recipes = await _bouquetService.GetAllRecipesAsync();
            return View(recipes);
        }

        [HttpGet]
        public async Task<IActionResult> Manage(int? id, int? bouquetProductId)
        {
            BouquetRecipeFormViewModel? model = null;

            if (id.HasValue && id.Value > 0)
            {
                model = await _bouquetService.GetRecipeByIdAsync(id.Value);
                if (model == null)
                {
                    TempData["ErrorMessage"] = "Recipe not found.";
                    return RedirectToAction(nameof(Index));
                }
            }
            else if (bouquetProductId.HasValue && bouquetProductId.Value > 0)
            {
                model = await _bouquetService.GetRecipeByBouquetProductIdAsync(bouquetProductId.Value);
            }

            model ??= new BouquetRecipeFormViewModel();

            await PopulateDropdownsAsync(model.BouquetProductId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(BouquetRecipeFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model.BouquetProductId);
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (success, errorMessage, recipeId) = await _bouquetService.SaveRecipeAsync(model, userId);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, errorMessage ?? "Failed to save bouquet recipe.");
                await PopulateDropdownsAsync(model.BouquetProductId);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Bouquet recipe successfully saved (Version {model.Version + (model.Id > 0 ? 1 : 0)}).";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Calculate(int bouquetProductId, decimal quantity = 1)
        {
            if (quantity <= 0) quantity = 1;

            var calculation = await _bouquetService.CalculateRequirementsAsync(bouquetProductId, quantity);
            if (calculation == null)
            {
                TempData["ErrorMessage"] = "No active recipe found for the specified bouquet product.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Warehouses = await _warehouseService.GetAllAsync(includeInactive: false);
            return View(calculation);
        }

        [HttpGet]
        public async Task<IActionResult> CalculateJson(int bouquetProductId, decimal quantity)
        {
            var calculation = await _bouquetService.CalculateRequirementsAsync(bouquetProductId, quantity);
            if (calculation == null)
                return NotFound(new { message = "Recipe not found." });

            return Json(calculation);
        }

        [HttpGet]
        public async Task<IActionResult> CheckAvailabilityJson(int bouquetProductId, decimal quantity, int warehouseId)
        {
            var availability = await _bouquetService.ValidateAvailabilityAsync(bouquetProductId, quantity, warehouseId);
            if (availability == null)
                return NotFound(new { message = "Recipe or warehouse not found." });

            return Json(availability);
        }

        [HttpGet]
        public async Task<IActionResult> Assemble(int? bouquetProductId)
        {
            var model = new AssembleBouquetsViewModel
            {
                BouquetProductId = bouquetProductId ?? 0,
                QuantityToAssemble = 1,
                IdempotencyToken = Guid.NewGuid().ToString()
            };

            ViewBag.BouquetProducts = await _bouquetService.GetAllRecipesAsync();
            ViewBag.Warehouses = await _warehouseService.GetAllAsync(includeInactive: false);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assemble(AssembleBouquetsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.BouquetProducts = await _bouquetService.GetAllRecipesAsync();
                ViewBag.Warehouses = await _warehouseService.GetAllAsync(includeInactive: false);
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (success, errorMessage) = await _bouquetService.AssembleBouquetsAsync(model, userId);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, errorMessage ?? "Failed to assemble bouquets.");
                ViewBag.BouquetProducts = await _bouquetService.GetAllRecipesAsync();
                ViewBag.Warehouses = await _warehouseService.GetAllAsync(includeInactive: false);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Successfully assembled {model.QuantityToAssemble} bouquet(s). Stock movement recorded.";
            return RedirectToAction("Index", "Inventory");
        }

        [HttpGet]
        public async Task<IActionResult> Snapshots(int bouquetProductId)
        {
            var snapshots = await _bouquetService.GetRecipeSnapshotsAsync(bouquetProductId);
            var recipe = await _bouquetService.GetRecipeByBouquetProductIdAsync(bouquetProductId);

            ViewBag.BouquetProductName = recipe?.BouquetProductName ?? "Bouquet Product";
            ViewBag.BouquetProductId = bouquetProductId;

            return View(snapshots);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var (success, errorMessage) = await _bouquetService.DeleteRecipeAsync(id);
            if (!success)
            {
                TempData["ErrorMessage"] = errorMessage ?? "Failed to delete recipe.";
            }
            else
            {
                TempData["SuccessMessage"] = "Bouquet recipe successfully deleted.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdownsAsync(int? currentBouquetProductId)
        {
            ViewBag.BouquetProducts = await _bouquetService.GetAvailableBouquetProductsAsync(currentBouquetProductId);
            ViewBag.ComponentProducts = await _bouquetService.GetAvailableComponentProductsAsync();
        }
    }
}
