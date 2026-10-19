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
    public class StocktakingController : Controller
    {
        private readonly IStocktakingService _stocktakingService;
        private readonly ApplicationDbContext _context;

        public StocktakingController(IStocktakingService stocktakingService, ApplicationDbContext context)
        {
            _stocktakingService = stocktakingService;
            _context = context;
        }

        public async Task<IActionResult> Index(int? warehouseId)
        {
            var sessions = await _stocktakingService.GetAllSessionsAsync(warehouseId);
            ViewBag.Warehouses = new SelectList(await _context.Warehouses.Where(w => w.IsActive).OrderBy(w => w.Name).ToListAsync(), "Id", "Name", warehouseId);
            ViewBag.SelectedWarehouseId = warehouseId;
            return View(sessions);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Warehouses = new SelectList(await _context.Warehouses.Where(w => w.IsActive).OrderBy(w => w.Name).ToListAsync(), "Id", "Name");
            return View(new StocktakingCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StocktakingCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Warehouses = new SelectList(await _context.Warehouses.Where(w => w.IsActive).OrderBy(w => w.Name).ToListAsync(), "Id", "Name", model.WarehouseId);
                return View(model);
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            var (success, error, sessionId) = await _stocktakingService.CreateSessionAsync(model, userId);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to create stocktaking session.");
                ViewBag.Warehouses = new SelectList(await _context.Warehouses.Where(w => w.IsActive).OrderBy(w => w.Name).ToListAsync(), "Id", "Name", model.WarehouseId);
                return View(model);
            }

            TempData["SuccessMessage"] = "Stocktaking session initiated. Please enter counted quantities.";
            return RedirectToAction(nameof(Count), new { id = sessionId });
        }

        [HttpGet]
        public async Task<IActionResult> Count(int id)
        {
            var session = await _stocktakingService.GetSessionForCountingAsync(id);
            if (session == null)
            {
                TempData["ErrorMessage"] = "Session not found or is already posted/cancelled.";
                return RedirectToAction(nameof(Index));
            }

            return View(session);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Count(StocktakingCountViewModel model)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            var (success, error) = await _stocktakingService.SaveCountsAsync(model, userId);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to save counts.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Counts recorded successfully. Ready for managerial review and approval.";
            return RedirectToAction(nameof(Review), new { id = model.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            var session = await _stocktakingService.GetSessionForReviewAsync(id);
            if (session == null)
            {
                TempData["ErrorMessage"] = "Session not found or already posted.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(session);
        }

        // 5. Restrict approval to authorized users (Admin & Manager)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPolicies.ApproveStocktaking)]
        public async Task<IActionResult> Approve(int id, string? approvalNotes, string? idempotencyToken)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            var (success, error) = await _stocktakingService.ApproveAndPostAdjustmentsAsync(id, approvalNotes, idempotencyToken, userId);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to approve and post stocktaking adjustments.";
                return RedirectToAction(nameof(Review), new { id });
            }

            TempData["SuccessMessage"] = "Stocktaking approved! Inventory adjustments posted to ledger.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            var (success, error) = await _stocktakingService.CancelSessionAsync(id, userId);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to cancel session.";
            }
            else
            {
                TempData["SuccessMessage"] = "Stocktaking session cancelled.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var session = await _stocktakingService.GetSessionDetailsAsync(id);
            if (session == null)
            {
                TempData["ErrorMessage"] = "Stocktaking session not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(session);
        }
    }
}
