using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Application.Interfaces;   // <-- DŮLEŽITÉ
using FitnessCenter.Web.Models;              // pokud mapuješ na vlastní ViewModel
using FitnessCenter.Domain.Entities;         // pokud vytváříš Lesson z viewmodelu

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Trainer")]
    public class LessonsController : Controller
    {
        private readonly ILessonsService _svc;   // <-- rozhraní

        public LessonsController(ILessonsService svc) // <-- rozhraní
            => _svc = svc;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.Active = "Lessons";
            ViewData["Title"] = "Lekce";

            var data = await _svc.GetAllAsync();  // <-- async/await

            // Pokud vlastní viewmodel, odmapuj:
            // var vm = data.Select(x => new LessonViewModel {
            //     Id = x.Id,
            //     Nazev = x.Nazev,
            //     Zacatek = x.Zacatek,
            //     Mistnost = x.Mistnost,
            //     Kapacita = x.Kapacita,
            //     Popis = x.Popis
            // }).ToList();
            // return View(vm);

            return View(data); // nebo vm – podle toho, co očekává view
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Active = "Lessons";
            ViewData["Title"] = "Nová lekce";
            return View(new LessonCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LessonCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Active = "Lessons";
                ViewData["Title"] = "Nová lekce";
                return View(model);
            }

            var lesson = new Lesson
            {
                Nazev = model.Nazev,
                Zacatek = model.Zacatek,
                Mistnost = model.Mistnost,
                Kapacita = model.Kapacita,
                Popis = model.Popis
            };

            await _svc.CreateAsync(lesson);

            TempData["Toast"] = "Lekce vytvořena.";
            return RedirectToAction(nameof(Index));
        }
    }
}
