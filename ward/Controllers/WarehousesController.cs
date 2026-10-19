using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ward.Constants;
using ward.Models.Inventory;
using ward.Services.Inventory;

namespace ward.Controllers
{
    [Authorize(Policy = AppPolicies.ManageInventory)]
    public class WarehousesController : Controller
    {
        private readonly IWarehouseService _warehouseService;

        public WarehousesController(IWarehouseService warehouseService)
        {
            _warehouseService = warehouseService;
        }

        public async Task<IActionResult> Index()
        {
            var warehouses = await _warehouseService.GetAllAsync(includeInactive: true);
            return View(warehouses);
        }

        public IActionResult Create()
        {
            return View(new WarehouseFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WarehouseFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (success, error) = await _warehouseService.CreateAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to create warehouse.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Warehouse '{model.Name}' ({model.Code.ToUpperInvariant()}) created successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var warehouse = await _warehouseService.GetByIdAsync(id);
            if (warehouse == null)
            {
                return NotFound();
            }

            return View(warehouse);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(WarehouseFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (success, error) = await _warehouseService.UpdateAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to update warehouse.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Warehouse '{model.Name}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var (success, error) = await _warehouseService.ToggleActiveAsync(id);
            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to update warehouse status.";
            }
            else
            {
                TempData["SuccessMessage"] = "Warehouse status updated successfully.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
