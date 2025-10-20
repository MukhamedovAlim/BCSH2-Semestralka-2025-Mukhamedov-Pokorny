using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    [Authorize] // zatím všichni
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.Active = "Home";

            // DEMO data permanentky
            ViewBag.PermFrom = DateTime.Today.AddDays(-7);     // začátek
            ViewBag.PermTo = DateTime.Today.AddDays(23);     // konec
            ViewBag.PermType = "Měsíční";
            ViewBag.PermPrice = "990 Kč";

            return View();
        }

    }
}
