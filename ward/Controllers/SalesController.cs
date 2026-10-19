using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ward.Constants;
using ward.Models.Sales;
using ward.Services.Sales;

namespace ward.Controllers
{
    [Authorize(Policy = AppPolicies.ProcessSales)]
    public class SalesController : Controller
    {
        private readonly ISalesService _salesService;

        public SalesController(ISalesService salesService)
        {
            _salesService = salesService;
        }

        public async Task<IActionResult> Index(SalesInvoiceFilterViewModel filter)
        {
            var model = await _salesService.GetSalesInvoicesPagedAsync(filter);
            ViewBag.Customers = await _salesService.GetAllCustomersAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _salesService.GetSalesInvoiceDetailsAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "Sales invoice not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(invoice);
        }

        [HttpGet]
        public async Task<IActionResult> Print(int id)
        {
            var invoice = await _salesService.GetSalesInvoiceDetailsAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "Sales invoice not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(invoice);
        }

        // Sales Returns Actions
        [HttpGet]
        public async Task<IActionResult> Returns()
        {
            var returns = await _salesService.GetAllSalesReturnsAsync();
            return View(returns);
        }

        [HttpGet]
        public async Task<IActionResult> CreateReturn(int invoiceId)
        {
            var model = await _salesService.PrepareCreateReturnViewModelAsync(invoiceId);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Invoice not found or is not eligible for return.";
                return RedirectToAction(nameof(Details), new { id = invoiceId });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReturn(SalesReturnCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            var (success, error, returnId) = await _salesService.ProcessSalesReturnAsync(model, userId);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to process sales return.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Sales return '{model.ReturnNumber}' processed successfully.";
            return RedirectToAction(nameof(ReturnDetails), new { id = returnId });
        }

        [HttpGet]
        public async Task<IActionResult> ReturnDetails(int id)
        {
            var returnDetails = await _salesService.GetSalesReturnDetailsAsync(id);
            if (returnDetails == null)
            {
                TempData["ErrorMessage"] = "Sales return not found.";
                return RedirectToAction(nameof(Returns));
            }

            return View(returnDetails);
        }
    }
}
