using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Web.Models;
using FitnessCenter.Infrastructure.Repositories;
using FitnessCenter.Web.Infrastructure.Security;

namespace FitnessCenter.Web.Controllers
{
    // Platby jsou “členská” sekce – admin se sem dostane jen přes emulaci
    [Authorize(Roles = "Member")]
    public class PaymentsController : Controller
    {
        private readonly PaymentsReadRepo _repo;
        public PaymentsController(PaymentsReadRepo repo) => _repo = repo;

        // Přehled plateb + stav členství
        public async Task<IActionResult> Index()
        {
            ViewBag.Active = "Payments";

            // ID aktuálního (nebo emulovaného) člena
            int memberId = User.GetRequiredCurrentMemberId();

            var ms = await _repo.GetMembershipAsync(memberId);
            ViewBag.ClenstviStav = ms.Active ? "Aktivní" : "Neaktivní";

            var rows = await _repo.GetPaymentsAsync(memberId);

            var vm = rows.Select(p => new PaymentViewModel
            {
                Datum = p.Datum,
                Popis = p.Popis,
                Castka = p.Castka,
                Stav = p.Stav
            }).ToList();

            return View(vm);
        }

        // Zobrazení nabídky permanentek
        [HttpGet]
        public IActionResult Buy()
        {
            ViewBag.Products = new[]
            {
                new { Name = "Měsíční",     Price =  990m },
                new { Name = "Roční",       Price = 7990m },
                new { Name = "Jednorázové", Price =  150m }
            };
            return View();
        }

        // Zpracování nákupu – ideálně podle memberId
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(string typ, decimal cena)
        {
            // zase ID aktuálního člena (ne e-mail)
            int memberId = User.GetRequiredCurrentMemberId();

            try
            {
                TempData["Ok"] = $"Platba za {typ} proběhla úspěšně.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Nákup selhal: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
