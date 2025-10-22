using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Trainer")]
    public class LessonsController : Controller
    {
        private readonly ILessonsService _lessons;

        public LessonsController(ILessonsService lessons)
        {
            _lessons = lessons;
        }

        // Správa lekcí (list)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var list = await _lessons.GetAllAsync();
            return View(list); // Views/Lessons/Index.cshtml
        }

        // Detail lekce
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var lesson = await _lessons.GetAsync(id);
            if (lesson == null) return NotFound();
            return View(lesson); // Views/Lessons/Detail.cshtml
        }

        // Docházka ke konkrétní lekci
        [HttpGet]
        public async Task<IActionResult> Attendance(int id)
        {
            var lesson = await _lessons.GetAsync(id);
            if (lesson == null) return NotFound();
            return View(lesson); // Views/Lessons/Attendance.cshtml
        }

        // Vytvoření lekce – formulář
        [HttpGet]
        public IActionResult Create()
        {
            var model = new Lesson
            {
                Zacatek = DateTime.Today.AddHours(18),
                Mistnost = "Sál A",
                Kapacita = 12
            };
            return View(model); // Views/Lessons/Create.cshtml
        }

        // Vytvoření lekce – uložení
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Lesson model)
        {
            if (!ModelState.IsValid) return View(model);

            var id = await _lessons.CreateAsync(model);
            return RedirectToAction(nameof(Detail), new { id });
        }

        // Úprava lekce – uložení
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Lesson model)
        {
            if (!ModelState.IsValid) return View("Detail", model);

            var ok = await _lessons.UpdateAsync(model);
            if (!ok) return NotFound();

            return RedirectToAction(nameof(Detail), new { id = model.Id });
        }
    }
}
