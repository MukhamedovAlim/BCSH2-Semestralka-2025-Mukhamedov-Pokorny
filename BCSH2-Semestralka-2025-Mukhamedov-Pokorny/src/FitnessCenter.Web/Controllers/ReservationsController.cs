using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    [Authorize]
    public class ReservationsController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.Active = "Reservations";
            return View();
        }
    }
}
