using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    [Authorize]
    public class PaymentsController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.Active = "Payments";
            return View();
        }
    }
}
