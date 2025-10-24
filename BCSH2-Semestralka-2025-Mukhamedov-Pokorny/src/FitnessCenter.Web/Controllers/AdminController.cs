using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.Active = "Admin";
            ViewBag.MembersCount = 0;
            ViewBag.TrainersCount = 0;
            ViewBag.LessonsToday = 0;
            ViewBag.PendingReservs = 0;
            return View();
        }
    }
}
