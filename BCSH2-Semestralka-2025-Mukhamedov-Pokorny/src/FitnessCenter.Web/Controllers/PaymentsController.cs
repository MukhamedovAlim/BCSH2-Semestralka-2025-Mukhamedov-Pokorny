using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Web.Models;

namespace FitnessCenter.Web.Controllers
{
    [Authorize]
    public class PaymentsController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.Active = "Payments";

            // DEMO data
            var history = new List<PaymentViewModel>
            {
                new() { Datum = DateTime.Today.AddDays(-30), Popis = "Členský poplatek – měsíční", Castka = 699, Stav = "Zaplaceno" },
                new() { Datum = DateTime.Today.AddDays(-60), Popis = "Překročení limitu návštěv", Castka = 150, Stav = "Zaplaceno" },
                new() { Datum = DateTime.Today.AddDays(-90), Popis = "Permanentka 3 měsíce",       Castka = 1590, Stav = "Zaplaceno" }
            };

            ViewBag.ClenstviStav = "Neaktivní"; // demo: žádná platná permanentka

            return View(history);
        }
    }
}
