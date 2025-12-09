using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models;
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
    [Authorize(Roles = "Trainer,Admin")]
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

        private async Task<int?> GetCurrentTrainerIdAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
                return null;

            return await _members.GetTrainerIdByEmailAsync(email);
        }

        // Správa lekcí (list) – pro trenéra
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId is null)
            {
                TempData["Err"] = "Tvůj účet není svázaný s žádným trenérem v databázi.";
                return View(Array.Empty<Lesson>());
            }

            var list = await _lessons.GetForTrainerAsync(trainerId.Value);
            return View(list);
        }

        // Docházka ke konkrétní lekci
        [HttpGet]
        public async Task<IActionResult> Attendance(int id, CancellationToken ct)
        {
            var lesson = await _lessons.GetAsync(id);
            if (lesson == null) return NotFound();

            var attendees = await _lessons.GetAttendeesAsync(id, ct);

            ViewBag.LessonId = id;
            return View(attendees);
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
            // VALIDACE – pole z entity Lesson necháme na DataAnnotations,
            // tady jen dohlídneme na výběr fitka
            if (SelectedFitnessCenterId is null || SelectedFitnessCenterId <= 0)
            {
                ModelState.AddModelError("SelectedFitnessCenterId", "Vyber fitness centrum.");
            }

            if (!ModelState.IsValid)
            {
                // znovu naplníme seznam fitek + předvybereme to, co uživatel zadal
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(
                    SelectedFitnessCenterId > 0 ? SelectedFitnessCenterId : null);
                ViewBag.SelectedFitnessCenterId = SelectedFitnessCenterId;
                return View(model);
            }

            // 2) trenér z claims
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId is null)
            {
                TempData["Err"] = "Tvůj účet není spojen s žádným trenérem v databázi.";
                return RedirectToAction(nameof(Create));
            }

            // 3) doplnění @NázevFitka do názvu
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

            // 4) uložit
            try
            {
                var id = await _lessons.CreateAsync(model, trainerId.Value);
                TempData["Ok"] = $"Lekce „{model.Nazev}“ byla úspěšně vytvořena.";
                return RedirectToAction(nameof(Index));
            }
            catch (OracleException ex) when (ex.Number == 20006 || ex.Number == 20007)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (OracleException ex)
            {
                ModelState.AddModelError(string.Empty, $"Databázová chyba ({ex.Number}): {ex.Message}");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Neočekávaná chyba: {ex.Message}");
            }

            // při chybě z DB znovu naplnit fitka + vrátit view
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(
                SelectedFitnessCenterId > 0 ? SelectedFitnessCenterId : null);
            ViewBag.SelectedFitnessCenterId = SelectedFitnessCenterId;
            return View(model);
        }


        // GET /Lessons/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var lesson = await _lessons.GetAsync(id);
            if (lesson == null) return NotFound();

            var vm = new LessonEditViewModel
            {
                Id = lesson.Id,
                Nazev = lesson.Nazev,
                Zacatek = lesson.Zacatek,
                Kapacita = lesson.Kapacita,
                Popis = lesson.Popis,

                SelectedFitnessCenterId = null
            };

            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(vm.SelectedFitnessCenterId);

            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LessonEditViewModel vm)
        {
            if (id != vm.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(vm.SelectedFitnessCenterId);
                return View(vm);
            }

            var lesson = await _lessons.GetAsync(id);
            if (lesson == null)
                return NotFound();

            lesson.Nazev = vm.Nazev.Trim();
            lesson.Zacatek = vm.Zacatek;
            lesson.Kapacita = vm.Kapacita;
            lesson.Popis = vm.Popis;

            // stejná logika jako v Create – přidání @Fitko do názvu
            if (vm.SelectedFitnessCenterId is > 0)
            {
                string? fcName = null;
                using (var con = await DatabaseManager.GetOpenConnectionAsync())
                using (var cmd = new OracleCommand(
                           "SELECT nazev FROM fitnesscentra WHERE idfitness=:id",
                           (OracleConnection)con)
                { BindByName = true })
                {
                    cmd.Parameters.Add("id", vm.SelectedFitnessCenterId.Value);
                    var obj = await cmd.ExecuteScalarAsync();
                    fcName = obj as string;
                }

                if (!string.IsNullOrWhiteSpace(fcName))
                {
                    var atIdx = lesson.Nazev?.LastIndexOf('@') ?? -1;
                    var baseName = (atIdx >= 0 ? lesson.Nazev![..atIdx].Trim()
                                               : lesson.Nazev?.Trim()) ?? "";
                    lesson.Nazev = $"{baseName} @{fcName}";
                }
            }

            try
            {
                var ok = await _lessons.UpdateAsync(lesson);
                if (!ok)
                {
                    ModelState.AddModelError(string.Empty, "Nepodařilo se uložit lekci.");
                    ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(vm.SelectedFitnessCenterId);
                    return View(vm);
                }

                TempData["Ok"] = "Lekce byla upravena.";
                return RedirectToAction(nameof(Index));
            }
            catch (OracleException ex) when (ex.Number == 20006 || ex.Number == 20007)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(vm.SelectedFitnessCenterId);
                return View(vm);
            }
            catch (OracleException ex)
            {
                ModelState.AddModelError(string.Empty, $"Databázová chyba ({ex.Number}): {ex.Message}");
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(vm.SelectedFitnessCenterId);
                return View(vm);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Neočekávaná chyba: {ex.Message}");
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync(vm.SelectedFitnessCenterId);
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete()
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId is null)
            {
                TempData["Err"] = "Tvůj účet není svázaný s žádným trenérem v databázi.";
                return View(Array.Empty<Lesson>());
            }

            var list = await _lessons.GetForTrainerAsync(trainerId.Value);
            return View(list);
        }

        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> RemoveMember(int lessonId, int memberId)
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var trainerId = await _members.GetTrainerIdByEmailAsync(email);

            if (trainerId is null)
            {
                TempData["Err"] = "K účtu není přiřazen žádný trenér.";
                return RedirectToAction(nameof(Attendance), new { id = lessonId });
            }

            try
            {
                await _lessons.RemoveMemberFromLessonAsync(lessonId, memberId, trainerId.Value);
                TempData["Ok"] = "Člen byl z lekce úspěšně odepsán.";
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Nepodařilo se odepsat člena: " + ex.Message;
            }

            return RedirectToAction(nameof(Attendance), new { id = lessonId });
        }


    }
}
