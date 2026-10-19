using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ward.Constants;
using ward.Models.Purchasing;
using ward.Services.Purchasing;

namespace ward.Controllers
{
    [Authorize(Policy = AppPolicies.ManageInventory)]
    public class SuppliersController : Controller
    {
        private readonly IPurchaseService _purchaseService;

        public SuppliersController(IPurchaseService purchaseService)
        {
            _purchaseService = purchaseService;
        }

        public async Task<IActionResult> Index()
        {
            var suppliers = await _purchaseService.GetAllSuppliersAsync();
            return View(suppliers);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new SupplierFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SupplierFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (success, error, _) = await _purchaseService.SaveSupplierAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to create supplier.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Supplier '{model.Name}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var supplier = await _purchaseService.GetSupplierByIdAsync(id);
            if (supplier == null)
            {
                TempData["ErrorMessage"] = "Supplier not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SupplierFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (success, error, _) = await _purchaseService.SaveSupplierAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to update supplier.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Supplier '{model.Name}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
