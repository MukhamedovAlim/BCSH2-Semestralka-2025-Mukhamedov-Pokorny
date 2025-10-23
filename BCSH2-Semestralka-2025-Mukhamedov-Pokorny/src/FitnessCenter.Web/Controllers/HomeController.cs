using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    public class HomeController : Controller
    {
        [Authorize(Roles = "Member")]
        public IActionResult Index()
        {
            ViewBag.Active = "Home";
            ViewBag.PermFrom = DateTime.Today.AddDays(-7);
            ViewBag.PermTo = DateTime.Today.AddDays(23);
            ViewBag.PermType = "Měsíční";
            ViewBag.PermPrice = "990 Kč";
            return View();
        }

        [Authorize(Roles = "Trainer")]
        public IActionResult Trainer()
        {
            ViewBag.Active = "HomeTrainer";
            ViewBag.Today = DateTime.Today;
            return View();
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Admin()
        {
            ViewBag.Active = "HomeAdmin";
            ViewBag.Today = DateTime.Today;
            return View();
        }
    }
}