using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Trainer")]
    public class LessonsController : Controller
    {
        private readonly ILessonsService _lessons;
        private readonly IMembersService _members;

        public LessonsController(ILessonsService lessons, IMembersService members)
        {
            _lessons = lessons;
            _members = members;
        }

        // ===== helper: načtení číselníku fitek do comboboxu =====
        private static async Task<List<SelectListItem>> LoadFitnessForSelectAsync(int? preselectId = null)
        {
            var items = new List<SelectListItem>();

            using var con = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(
                "SELECT idfitness, nazev FROM fitnesscentra ORDER BY nazev",
                (OracleConnection)con);

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var id = rd.GetInt32(0);
                var nazev = rd.GetString(1);
                items.Add(new SelectListItem
                {
                    Value = id.ToString(),
                    Text = nazev,
                    Selected = preselectId.HasValue && preselectId.Value == id
                });
            }

            return items;
        }

        // Správa lekcí (list)
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
            return View(lesson);
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
        public async Task<IActionResult> Create()
        {
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();

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
        public async Task<IActionResult> Create(Lesson model, [FromForm] int? SelectedFitnessCenterId)
        {
            // 1) Validace
            if (string.IsNullOrWhiteSpace(model.Nazev))
                ModelState.AddModelError(nameof(model.Nazev), "Název je povinný.");
            if (model.Kapacita <= 0)
                ModelState.AddModelError(nameof(model.Kapacita), "Kapacita musí být > 0.");
            if (model.Zacatek == default)
                ModelState.AddModelError(nameof(model.Zacatek), "Zadej platný datum a čas.");

            if (!ModelState.IsValid)
            {
                // znovu naplníme combobox + zachováme výběr
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(SelectedFitnessCenterId);
                ViewBag.SelectedFitnessCenterId = SelectedFitnessCenterId;
                return View(model);
            }

            // 2) Trenér z claims
            var email = User.FindFirstValue(ClaimTypes.Email);
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

            // 3) Pokud trenér vybral fitko v comboboxu, doplníme @NázevFitka do názvu lekce
            if (SelectedFitnessCenterId is > 0)
            {
                string? fcName = null;
                using (var con = await DatabaseManager.GetOpenConnectionAsync())
                using (var cmd = new OracleCommand(
                    "SELECT nazev FROM fitnesscentra WHERE idfitness=:id",
                    (OracleConnection)con)
                { BindByName = true })
                {
                    cmd.Parameters.Add("id", SelectedFitnessCenterId);
                    var obj = await cmd.ExecuteScalarAsync();
                    fcName = obj as string;
                }

                if (!string.IsNullOrWhiteSpace(fcName))
                {
                    // aby se @xxx nezdvojoval
                    var atIdx = model.Nazev?.LastIndexOf('@') ?? -1;
                    var baseName = (atIdx >= 0 ? model.Nazev![..atIdx].Trim() : model.Nazev?.Trim()) ?? "";
                    model.Nazev = $"{baseName} @{fcName}";
                }
            }

            // 4) Uložení
            try
            {
                var id = await _lessons.CreateAsync(model, trainerId.Value);
                TempData["Ok"] = $"Lekce „{model.Nazev}“ byla úspěšně vytvořena (ID {id}).";
                return RedirectToAction(nameof(Create)); // nebo RedirectToAction(nameof(Detail), new { id })
            }
            catch (Exception ex)
            {
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
            return View(list); // Views/Lessons/Delete.cshtml
        }
    }
}
