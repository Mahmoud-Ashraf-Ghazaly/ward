using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using ward.Models;

namespace ward.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode = null)
        {
            var message = statusCode switch
            {
                404 => "The page or resource you requested could not be found.",
                403 => "Access denied. You do not have permission to access this resource.",
                500 => "An internal server error occurred while processing your request.",
                _ => "An error occurred while processing your request."
            };

            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = statusCode,
                ErrorMessage = message
            });
        }
    }
}
