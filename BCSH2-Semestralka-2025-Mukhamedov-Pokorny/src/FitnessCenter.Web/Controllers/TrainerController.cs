using FitnessCenter.Infrastructure.Repositories; // OracleLessonsRepository
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Trainer,Admin")]
    public class TrainerController : Controller
    {
        private readonly OracleLessonsRepository _lessons;

        public TrainerController(OracleLessonsRepository lessons)
        {
            _lessons = lessons;
        }

        private int? GetTrainerIdFromClaims()
        {
            var val = User.FindFirst("TrainerId")?.Value; // claim přidaný při loginu
            return int.TryParse(val, out var id) ? id : (int?)null;
        }

        /// <summary>
        /// /Trainer/Index – nadcházející lekce daného trenéra (procedura SP_LESSONS_UPCOMING)
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var trainerId = GetTrainerIdFromClaims();
            if (trainerId is null)
            {
                TempData["Err"] = "K účtu není přiřazen žádný trenér.";
                return RedirectToAction("Index", "Home");
            }

            var list = await _lessons.GetUpcomingViaProcAsync(DateTime.Now, trainerId);
            ViewBag.Active = "Trainer";
            return View(list); // @model IReadOnlyList<FitnessCenter.Domain.Entities.Lesson>
        }

        /// <summary>
        /// /Trainer/MojeLekce – všechny lekce daného trenéra (pro zrušení)
        /// </summary>
        public async Task<IActionResult> MojeLekce(CancellationToken ct)
        {
            var trainerId = GetTrainerIdFromClaims();
            if (trainerId is null)
            {
                TempData["Err"] = "K účtu není přiřazen žádný trenér.";
                return RedirectToAction("Index", "Home");
            }

            var list = await _lessons.GetForTrainerAsync(trainerId.Value, ct);
            ViewBag.Active = "Trainer";
            return View(list); // @model IReadOnlyList<FitnessCenter.Domain.Entities.Lesson>
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Trainer/ZrusitLekci")]
        public async Task<IActionResult> ZrusitLekci([FromForm] int id, CancellationToken ct)
        {
            int? trainerId = GetTrainerIdFromClaims();

            // Fallback: když nemám TrainerId claim, zkusím ho najít podle e-mailu v DB
            if (trainerId is null)
            {
                var email = User.FindFirstValue(ClaimTypes.Email);
                if (!string.IsNullOrWhiteSpace(email))
                {
                    var t = await _lessons.GetTrainerIdByEmailAsync(email); // tahle metoda už v repu je
                    if (t.HasValue) trainerId = t.Value;
                }
            }

            if (trainerId is null)
            {
                TempData["Err"] = "Nepodařilo se zjistit trenéra z přihlášení.";
                return RedirectToAction("Delete", "Lessons");
            }

            try
            {
                var zrusenoRez = await _lessons.CancelLessonByTrainerAsync(id, trainerId.Value, ct);
                TempData["Ok"] = $"Lekce byla zrušena.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Err"] = ex.Message; // např. -20060 z DB
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Zrušení lekce selhalo: " + ex.Message;
            }

            // Zůstaneme na seznamu mazání (a ukážeme hlášku)
            return RedirectToAction("Delete", "Lessons");
        }

    }
}
