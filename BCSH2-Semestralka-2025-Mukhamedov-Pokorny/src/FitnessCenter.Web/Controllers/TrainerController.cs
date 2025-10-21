using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Trainer")]
    public class TrainerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
