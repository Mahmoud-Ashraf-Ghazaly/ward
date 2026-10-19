using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ward.Constants;
using ward.Models.Sales;
using ward.Services.Catalog;
using ward.Services.Inventory;
using ward.Services.Sales;

namespace ward.Controllers
{
    [Authorize(Policy = AppPolicies.ProcessSales)]
    public class PosController : Controller
    {
        private readonly ISalesService _salesService;
        private readonly IWarehouseService _warehouseService;
        private readonly ICategoryService _categoryService;

        public PosController(
            ISalesService salesService,
            IWarehouseService warehouseService,
            ICategoryService categoryService)
        {
            _salesService = salesService;
            _warehouseService = warehouseService;
            _categoryService = categoryService;
        }

        public async Task<IActionResult> Index()
        {
            var warehouses = await _warehouseService.GetAllAsync(includeInactive: false);
            var categories = await _categoryService.GetAllAsync();
            var customers = await _salesService.GetAllCustomersAsync();

            ViewBag.Warehouses = warehouses;
            ViewBag.Categories = categories;
            ViewBag.Customers = customers;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts(int warehouseId, string? searchTerm = null, int? categoryId = null)
        {
            var products = await _salesService.GetPosProductsAsync(warehouseId, searchTerm, categoryId);
            return Json(products);
        }

        [HttpGet]
        public async Task<IActionResult> Scan(string code, int warehouseId)
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { message = "Barcode or SKU is required." });

            var product = await _salesService.FindProductForPosAsync(code, warehouseId);
            if (product == null)
                return NotFound(new { message = $"No active product found matching '{code}'." });

            return Json(product);
        }

        [HttpPost]
        public async Task<IActionResult> Checkout([FromBody] PosCheckoutRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new PosCheckoutResultDto { Success = false, ErrorMessage = "Invalid checkout data." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _salesService.ProcessCheckoutAsync(request, userId);

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> Receipt(int id)
        {
            var invoice = await _salesService.GetSalesInvoiceDetailsAsync(id);
            if (invoice == null)
            {
                TempData["ErrorMessage"] = "Invoice not found.";
                return RedirectToAction("Index", "Sales");
            }

            return View(invoice);
        }
    }
}
