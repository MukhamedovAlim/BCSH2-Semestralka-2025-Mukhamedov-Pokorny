using System;
using System.Threading.Tasks;
using FitnessCenter.Infrastructure.Repositories; // OracleLessonsRepository
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
            var val = User.FindFirst("TrainerId")?.Value; // claim jsme přidávali při loginu
            return int.TryParse(val, out var id) ? id : (int?)null;
        }

        // /Trainer/Index – ukázkově: nadcházející lekce daného trenéra z procedury SP_LESSONS_UPCOMING
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
    }
}
