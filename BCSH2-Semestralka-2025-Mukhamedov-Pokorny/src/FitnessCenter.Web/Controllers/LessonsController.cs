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
            return View(list);
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
            return View(model);
        }

        // Vytvoření lekce – uložení
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

            // 3) doplnění @NázevFitka do názvu (volitelné)
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
                    var atIdx = model.Nazev?.LastIndexOf('@') ?? -1;
                    var baseName = (atIdx >= 0 ? model.Nazev![..atIdx].Trim() : model.Nazev?.Trim()) ?? "";
                    model.Nazev = $"{baseName} @{fcName}";
                }
            }

            // 4) Uložení s ošetřením chyb z triggeru
            try
            {
                var id = await _lessons.CreateAsync(model, trainerId.Value);
                TempData["Ok"] = $"Lekce „{model.Nazev}“ byla úspěšně vytvořena.";
                return RedirectToAction(nameof(Index));
            }
            catch (OracleException ex) when (ex.Number == 20006 || ex.Number == 20007)
            {
                // 20006 = kapacita < 1, 20007 = snížení pod přihlášené (u insertu spíš 20006)
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(SelectedFitnessCenterId);
                ViewBag.SelectedFitnessCenterId = SelectedFitnessCenterId;
                return View(model);
            }
            catch (OracleException ex)
            {
                ModelState.AddModelError(string.Empty, $"Databázová chyba ({ex.Number}): {ex.Message}");
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(SelectedFitnessCenterId);
                ViewBag.SelectedFitnessCenterId = SelectedFitnessCenterId;
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Neočekávaná chyba: {ex.Message}");
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(SelectedFitnessCenterId);
                ViewBag.SelectedFitnessCenterId = SelectedFitnessCenterId;
                return View(model);
            }
        }

        // Edit formulář (GET)
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var lesson = await _lessons.GetAsync(id);
            if (lesson == null) return NotFound();

            // pokud používáš combobox na fitka i v Editu:
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
            return View(lesson);
        }

        // Úprava lekce – uložení
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Lesson model)
        {
            if (id != model.Id) return NotFound();
            if (!ModelState.IsValid)
            {
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(model);
            }

            try
            {
                var ok = await _lessons.UpdateAsync(model);
                if (ok)
                {
                    TempData["Ok"] = "Lekce byla upravena.";
                    return RedirectToAction(nameof(Detail), new { id = model.Id });
                }

                ModelState.AddModelError(string.Empty, "Nepodařilo se uložit lekci.");
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(model);
            }
            catch (OracleException ex) when (ex.Number == 20006 || ex.Number == 20007)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(model);
            }
            catch (OracleException ex)
            {
                ModelState.AddModelError(string.Empty, $"Databázová chyba ({ex.Number}): {ex.Message}");
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Neočekávaná chyba: {ex.Message}");
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(model);
            }
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
            return View(list);
        }
    }
}
