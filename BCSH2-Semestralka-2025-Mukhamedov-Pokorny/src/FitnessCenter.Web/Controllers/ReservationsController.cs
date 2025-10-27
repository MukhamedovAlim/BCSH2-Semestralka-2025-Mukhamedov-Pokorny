using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Infrastructure.Repositories;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Member")]
    [Route("[controller]")] // => /Reservations/...
    public class ReservationsController : Controller
    {
        private readonly OracleLessonsRepository _repo;
        private readonly PaymentsReadRepo _members;

        public ReservationsController(OracleLessonsRepository repo, PaymentsReadRepo members)
        {
            _repo = repo;
            _members = members;
        }

        private async Task<int> ResolveMemberId()
        {
            if (int.TryParse(User.FindFirst("ClenId")?.Value, out var id) && id > 0)
                return id;

            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrWhiteSpace(email)) return 0;

            return (await _members.GetMemberIdByEmailAsync(email)) ?? 0;
        }

        // GET /Reservations
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var list = await _repo.GetUpcomingViaProcAsync(); // procedura SP_LESSONS_UPCOMING
            return View(list); // @model IReadOnlyList<Lesson>
        }

        // GET /Reservations/Mine
        [HttpGet("Mine")]
        public async Task<IActionResult> Mine()
        {
            var idClen = await ResolveMemberId();
            var rows = await _repo.GetMyReservationsViaProcAsync(idClen); // SP_MY_RESERVATIONS
            return View(rows); // @model List<OracleLessonsRepository.MyReservationRow>
        }

        // POST /Reservations/Book/{id}
        [HttpPost("Book/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(int id)
        {
            var idClen = await ResolveMemberId();
            try
            {
                var idRez = await _repo.ReserveLessonAsync(idClen, id);
                TempData["ResMsg"] = $"Rezervace vytvořena (#{idRez}).";
                return RedirectToAction(nameof(Mine));
            }
            catch (Exception ex)
            {
                TempData["ResMsg"] = "Rezervaci se nepodařilo vytvořit: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST /Reservations/Cancel
        [HttpPost("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel([FromForm] int rezId)
        {
            var idClen = await ResolveMemberId();
            await _repo.CancelReservationByIdAsync(idClen, rezId);
            TempData["ResMsg"] = "Rezervace byla zrušena.";
            return RedirectToAction(nameof(Mine));
        }

    }
}
