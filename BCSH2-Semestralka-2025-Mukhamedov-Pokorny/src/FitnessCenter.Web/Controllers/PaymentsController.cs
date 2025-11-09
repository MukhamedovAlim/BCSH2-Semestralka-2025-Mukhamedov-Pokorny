using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Web.Models;
using FitnessCenter.Infrastructure.Repositories;

namespace FitnessCenter.Web.Controllers
{
    [Authorize]
    public class PaymentsController : Controller
    {
        private readonly PaymentsReadRepo _repo;
        public PaymentsController(PaymentsReadRepo repo) => _repo = repo;

        public async Task<IActionResult> Index()
        {
            ViewBag.Active = "Payments";

            // 1) pokus o ClenId z claimu
            int clenId = 0;
            int.TryParse(User.FindFirst("ClenId")?.Value, out clenId);

            // 2) fallback: zjisti podle e-mailu
            if (clenId == 0)
            {
                var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    var resolved = await _repo.GetMemberIdByEmailAsync(email);
                    clenId = resolved ?? 0;
                }
            }

            // Stav členství + historie
            var ms = await _repo.GetMembershipAsync(clenId);
            ViewBag.ClenstviStav = ms.Active ? "Aktivní" : "Neaktivní";

            var rows = clenId == 0 ? new List<PaymentsReadRepo.PaymentRow>()
                                   : await _repo.GetPaymentsAsync(clenId);

            var vm = rows.Select(p => new PaymentViewModel
            {
                Datum = p.Datum,
                Popis = p.Popis,
                Castka = p.Castka,
                Stav = p.Stav
            }).ToList();

            return View(vm);
        }

        [HttpGet]
        public IActionResult Buy()
        {
            ViewBag.Products = new[]
            {
        new { Name = "Měsíční",    Price = 990m },
        new { Name = "Roční",      Price = 7990m },
        new { Name = "Jednorázové",Price = 150m }
    };
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(string typ, decimal cena)
        {
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Nepodařilo se zjistit e-mail přihlášeného uživatele.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var (idCl, idPl) = await _repo.PurchaseMembershipAsync(email, typ, cena);
                TempData["Ok"] = $"Zakoupeno: {typ}.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Nákup selhal: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

    }
}
