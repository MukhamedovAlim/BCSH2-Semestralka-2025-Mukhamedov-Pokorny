using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    [Authorize]
    public class EquipmentController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.Active = "Equipment";
            return View();
        }
    }
}
