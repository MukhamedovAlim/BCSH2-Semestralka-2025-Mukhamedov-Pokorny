using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Application.Interfaces;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Member")]
    public class ReservationsController : Controller
    {
        private readonly ILessonsService _lessons;

        public ReservationsController(ILessonsService lessons)
        {
            _lessons = lessons;
        }

        // GET: /Reservations
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var lessons = await _lessons.GetAllAsync();
            return View(lessons); // Views/Reservations/Index.cshtml
        }

        // GET: /Reservations/Mine
        [HttpGet]
        public IActionResult Mine()
        {
            // Placeholder – vlastní rezervace uživatele sem přidáme později
            return View(); // Views/Reservations/Mine.cshtml
        }

        // POST: /Reservations/Book/5  (zatím jen ukázka)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Book(int id)
        {
            TempData["ResMsg"] = $"Rezervace lekce #{id} byla vytvořena (demo).";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Reservations/Cancel/5  (zatím jen ukázka)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel(int id)
        {
            TempData["ResMsg"] = $"Rezervace lekce #{id} byla zrušena (demo).";
            return RedirectToAction(nameof(Mine));
        }
    }
}
