using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Trainer")]
    public class MessagesController : Controller
    {
        [HttpGet]
        public IActionResult Index() => View(); // Views/Messages/Index.cshtml
    }
}
