using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Web.Models;
using FitnessCenter.Infrastructure.Repositories;
using System.Security.Claims;

namespace FitnessCenter.Web.Controllers
{
    [Authorize]
    public class PaymentsController : Controller
    {
        private readonly PaymentsReadRepo _repo;
        public PaymentsController(PaymentsReadRepo repo) => _repo = repo;

        // 🧾 Přehled plateb + stav členství
        public async Task<IActionResult> Index()
        {
            ViewBag.Active = "Payments";

            int clenId = 0;
            int.TryParse(User.FindFirst("ClenId")?.Value, out clenId);

            if (clenId == 0)
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (!string.IsNullOrWhiteSpace(email))
                    clenId = await _repo.GetMemberIdByEmailAsync(email) ?? 0;
            }

            var ms = await _repo.GetMembershipAsync(clenId);
            ViewBag.ClenstviStav = ms.Active ? "Aktivní" : "Neaktivní";

            var rows = clenId == 0
                ? new List<PaymentsReadRepo.PaymentRow>()
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

        // 💳 Zobrazení nabídky permanentek
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

        // 🧾 Zpracování nákupu (volání PL/SQL procedury)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(string typ, decimal cena)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Nepodařilo se zjistit e-mail přihlášeného uživatele.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // volání procedury v repozitáři
                await _repo.PurchaseMembershipAsync(email, typ, cena);
                TempData["Ok"] = $"Platba za {typ} proběhla úspěšně.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Nákup selhal: " + ex.Message;
            }

            // 🔁 návrat zpět na přehled plateb
            return RedirectToAction(nameof(Index));
        }
    }
}
