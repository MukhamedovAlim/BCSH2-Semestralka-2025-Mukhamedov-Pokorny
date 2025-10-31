using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Trainer")]
    public class LessonsController : Controller
    {
        private readonly ILessonsService _lessons;
        private readonly IMembersService _members;
        private OracleLessonsRepository _repo;

        public LessonsController(ILessonsService lessons, IMembersService members)
        {
            _lessons = lessons;
            _members = members;
        }

        // Správa lekcí (list)
        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // zjistíme ID trenéra z e-mailu přihlášeného uživatele
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Err"] = "Nelze zjistit e-mail přihlášeného uživatele.";
                return View(Array.Empty<Lesson>());
            }

            // přes MembersService si necháme vrátit TrainerId
            var trainerId = await _members.GetTrainerIdByEmailAsync(email);
            if (trainerId is null)
            {
                TempData["Err"] = "Tvůj účet není svázaný s žádným trenérem v databázi.";
                return View(Array.Empty<Lesson>());
            }

            var list = await _lessons.GetForTrainerAsync(trainerId.Value);
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
        public async Task<IActionResult> Attendance(int id, CancellationToken ct)
        {
            var lesson = await _lessons.GetAsync(id);
            if (lesson == null) return NotFound();

            var emails = await _lessons.GetAttendeeEmailsAsync(id, ct);
            ViewBag.Attendees = emails;
            ViewBag.AttendeesCount = emails.Count;

            return View(lesson);
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

        // Vytvoření lekce – uložení (spáruje přihlášeného trenéra podle e-mailu)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Lesson model)
        {
            // 1) Základní validace aby se vždy ukázalo proč to padá
            if (string.IsNullOrWhiteSpace(model.Nazev))
                ModelState.AddModelError(nameof(model.Nazev), "Název je povinný.");
            if (model.Kapacita <= 0)
                ModelState.AddModelError(nameof(model.Kapacita), "Kapacita musí být > 0.");
            if (model.Zacatek == default)
                ModelState.AddModelError(nameof(model.Zacatek), "Zadej platný datum a čas.");

            // 2) Když je ModelState nevalidní -> sebereme chyby, dáme do TempData a PRG na GET /Create
            if (!ModelState.IsValid)
            {
                TempData["Err"] = string.Join(" | ", ModelState
                    .Where(kv => kv.Value?.Errors?.Count > 0)
                    .SelectMany(kv => kv.Value!.Errors.Select(e => $"{kv.Key}: {e.ErrorMessage}")));
                return RedirectToAction(nameof(Create));
            }

            // 3) Dohledat trenéra podle e-mailu z claims
            var email = User.FindFirstValue(System.Security.Claims.ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Err"] = "Nelze zjistit e-mail přihlášeného uživatele.";
                return RedirectToAction(nameof(Create));
            }

            var trainerId = await _members.GetTrainerIdByEmailAsync(email);
            if (trainerId is null)
            {
                TempData["Err"] = "Tvůj účet není spojen s žádným trenérem v databázi.";
                return RedirectToAction(nameof(Create));
            }

            try
            {
                // 4) Uložení (repo vkládá do: NAZEVLEKCE, DATUMLEKCE, OBSAZENOST, TRENER_IDTRENER)
                var id = await _lessons.CreateAsync(model, trainerId.Value);

                // 5) Úspěch -> zelená hláška + PRG (můžeš přesměrovat i na Detail)
                TempData["Ok"] = $"Lekce „{model.Nazev}“ byla úspěšně vytvořena (ID {id}).";
                return RedirectToAction(nameof(Create));               // nebo: return RedirectToAction(nameof(Detail), new { id });
                                                                       // return RedirectToAction("Trainer", "Home");
            }
            catch (Exception ex)
            {
                // 6) Chyba z DB -> do TempData a PRG zpět na GET
                TempData["Err"] = $"Chyba při ukládání: {ex.Message}";
                return RedirectToAction(nameof(Create));
            }
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

        [HttpGet]
        public async Task<IActionResult> Delete()
        {
            // zjistíme trenéra z e-mailu přihlášeného uživatele
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Err"] = "Nelze zjistit e-mail přihlášeného uživatele.";
                return View(Array.Empty<Lesson>());
            }

            var trainerId = await _members.GetTrainerIdByEmailAsync(email);
            if (trainerId is null)
            {
                TempData["Err"] = "Tvůj účet není svázaný s žádným trenérem v databázi.";
                return View(Array.Empty<Lesson>());
            }

            var list = await _lessons.GetForTrainerAsync(trainerId.Value);
            return View(list); // Views/Lessons/Delete.cshtml s @model IReadOnlyList<Lesson>
        }
    }
}
