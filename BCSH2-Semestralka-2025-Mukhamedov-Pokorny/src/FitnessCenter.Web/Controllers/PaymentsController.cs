using System.Threading;
using FitnessCenter.Infrastructure.Repositories;
using FitnessCenter.Web.Infrastructure.Security;
using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    // Platby jsou “členská” sekce – admin se sem dostane jen přes emulaci
    [Authorize(Roles = "Member")]
    public class PaymentsController : Controller
    {
        private readonly PaymentsReadRepo _read;
        private readonly PaymentsWriteRepo _write;
        public PaymentsController(PaymentsReadRepo read, PaymentsWriteRepo write)
        {
            _read = read;
            _write = write;
        }

        // Přehled plateb + stav členství
        public async Task<IActionResult> Index()
        {
            ViewBag.Active = "Payments";

            // ID aktuálního (nebo emulovaného) člena
            int memberId = User.GetRequiredCurrentMemberId();

            var ms = await _read.GetMembershipAsync(memberId);
            ViewBag.ClenstviStav = ms.Active ? "Aktivní" : "Neaktivní";

            var rows = await _read.GetPaymentsAsync(memberId);

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(string typ, decimal cena)
        {
            int memberId = User.GetRequiredCurrentMemberId();

            if (string.IsNullOrWhiteSpace(typ) || cena <= 0)
            {
                TempData["Error"] = "Neplatná data pro nákup.";
                return RedirectToAction(nameof(Buy));
            }

            try
            {
                var newPaymentId = await _write.CreatePaymentAsync(
                    memberId: memberId,
                    amount: cena,
                    stavNazev: "Vyřizuje se",
                    datum: DateTime.Now
                );

                TempData["Ok"] = $"Platba za {typ} byla založena (ID: {newPaymentId}) a čeká na schválení.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = "Nákup selhal: " + ex.Message;
            }
            catch (Exception)
            {
                TempData["Error"] = "Nákup selhal z neočekávaného důvodu.";
            }

            return RedirectToAction(nameof(Index));
        }


    }
}
