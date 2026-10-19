using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ward.Constants;
using ward.Models.Sales;
using ward.Services.Sales;

namespace ward.Controllers
{
    [Authorize(Policy = AppPolicies.ProcessSales)]
    public class CustomersController : Controller
    {
        private readonly ISalesService _salesService;

        public CustomersController(ISalesService salesService)
        {
            _salesService = salesService;
        }

        public async Task<IActionResult> Index()
        {
            var customers = await _salesService.GetAllCustomersAsync();
            return View(customers);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CustomerFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (success, error, _) = await _salesService.SaveCustomerAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to create customer.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Customer '{model.Name}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> QuickCreateAjax([FromBody] CustomerFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest(new { message = "Customer name is required." });
            }

            var (success, error, customerId) = await _salesService.SaveCustomerAsync(model);
            if (!success)
            {
                return BadRequest(new { message = error });
            }

            return Json(new { id = customerId, name = model.Name });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var customer = await _salesService.GetCustomerByIdAsync(id);
            if (customer == null)
            {
                TempData["ErrorMessage"] = "Customer not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CustomerFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (success, error, _) = await _salesService.SaveCustomerAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to update customer.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Customer '{model.Name}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
