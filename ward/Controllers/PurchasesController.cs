using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ward.Constants;
using ward.Entities.Enums;
using ward.Models.Purchasing;
using ward.Services.Catalog;
using ward.Services.Inventory;
using ward.Services.Purchasing;

namespace ward.Controllers
{
    [Authorize(Policy = AppPolicies.ManageInventory)]
    public class PurchasesController : Controller
    {
        private readonly IPurchaseService _purchaseService;
        private readonly IWarehouseService _warehouseService;
        private readonly IProductService _productService;

        public PurchasesController(
            IPurchaseService purchaseService,
            IWarehouseService warehouseService,
            IProductService productService)
        {
            _purchaseService = purchaseService;
            _warehouseService = warehouseService;
            _productService = productService;
        }

        public async Task<IActionResult> Index(PurchaseInvoiceFilterViewModel filter)
        {
            var model = await _purchaseService.GetInvoicesPagedAsync(filter);
            ViewBag.Suppliers = await _purchaseService.GetAllSuppliersAsync();
            ViewBag.Warehouses = await _warehouseService.GetAllAsync(includeInactive: false);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = await _purchaseService.PrepareCreateInvoiceViewModelAsync();
            await PopulateCreateDropdownsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseInvoiceCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateCreateDropdownsAsync();
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (success, error, invoiceId) = await _purchaseService.CreateInvoiceAsync(model, userId);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to create purchase invoice.");
                await PopulateCreateDropdownsAsync();
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                // Warning note (e.g. created draft but posting failed)
                TempData["ErrorMessage"] = error;
            }
            else
            {
                TempData["SuccessMessage"] = model.PostImmediately 
                    ? $"Purchase invoice '{model.InvoiceNumber}' created and posted successfully. Warehouse stock increased."
                    : $"Purchase invoice '{model.InvoiceNumber}' saved as Draft.";
            }

            return RedirectToAction(nameof(Details), new { id = invoiceId });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _purchaseService.GetInvoiceDetailsAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "Purchase invoice not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(invoice);
        }

        [HttpGet]
        public async Task<IActionResult> Print(int id)
        {
            var invoice = await _purchaseService.GetInvoiceDetailsAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "Purchase invoice not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(invoice);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Post(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (success, error) = await _purchaseService.PostInvoiceAsync(id, userId);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to post purchase invoice.";
            }
            else
            {
                TempData["SuccessMessage"] = "Purchase invoice successfully posted! Stock and supplier balance updated.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (success, error) = await _purchaseService.CancelInvoiceAsync(id, userId);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to cancel invoice.";
            }
            else
            {
                TempData["SuccessMessage"] = "Draft purchase invoice successfully cancelled.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> CreateReturn(int invoiceId)
        {
            var model = await _purchaseService.PrepareCreateReturnViewModelAsync(invoiceId);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Invoice is not eligible for returns or was not found.";
                return RedirectToAction(nameof(Details), new { id = invoiceId });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReturn(PurchaseReturnCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (success, error, returnId) = await _purchaseService.CreateAndProcessReturnAsync(model, userId);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to process purchase return.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Purchase return '{model.ReturnNumber}' successfully processed! Warehouse stock decreased and supplier balance credited.";
            return RedirectToAction(nameof(ReturnDetails), new { id = returnId });
        }

        [HttpGet]
        public async Task<IActionResult> ReturnDetails(int id)
        {
            var ret = await _purchaseService.GetReturnDetailsAsync(id);
            if (ret == null)
            {
                TempData["ErrorMessage"] = "Purchase return not found.";
                return RedirectToAction(nameof(Returns));
            }

            return View(ret);
        }

        [HttpGet]
        public async Task<IActionResult> Returns()
        {
            var returns = await _purchaseService.GetAllReturnsAsync();
            return View(returns);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPayment(SupplierPaymentFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid payment data. Amount must be greater than zero.";
                if (model.PurchaseInvoiceId.HasValue)
                {
                    return RedirectToAction(nameof(Details), new { id = model.PurchaseInvoiceId.Value });
                }
                return RedirectToAction("Index", "Suppliers");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (success, error) = await _purchaseService.RecordSupplierPaymentAsync(model, userId);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to record payment.";
            }
            else
            {
                TempData["SuccessMessage"] = $"Successfully recorded payment of ${model.Amount:F2} to {model.SupplierName}.";
            }

            if (model.PurchaseInvoiceId.HasValue)
            {
                return RedirectToAction(nameof(Details), new { id = model.PurchaseInvoiceId.Value });
            }
            return RedirectToAction("Index", "Suppliers");
        }

        private async Task PopulateCreateDropdownsAsync()
        {
            ViewBag.Suppliers = await _purchaseService.GetAllSuppliersAsync();
            ViewBag.Warehouses = await _warehouseService.GetAllAsync(includeInactive: false);
            var pagedProducts = await _productService.GetPagedProductsAsync(new Models.Catalog.ProductListFilterViewModel { PageSize = 1000, OnlyActive = true });
            ViewBag.Products = pagedProducts.Items.Where(p => p.ProductType != ProductType.Service).ToList();
        }
    }
}
