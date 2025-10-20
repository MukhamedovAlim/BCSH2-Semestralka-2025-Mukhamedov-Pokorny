using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Web.Models;

namespace FitnessCenter.Web.Controllers
{
    [Authorize]
    public class ReservationsController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.Active = "Reservations";

            // DEMO data
            var list = new List<ReservationViewModel>
            {
                new() { Id = 1, Nazev = "Crossfit",      Datum = DateTime.Today.AddDays(1).AddHours(18), Kapacita = 12, Prihlaseno = 9,  JsemPrihlasen = true },
                new() { Id = 2, Nazev = "Spinning",      Datum = DateTime.Today.AddDays(2).AddHours(17), Kapacita = 15, Prihlaseno = 7,  JsemPrihlasen = false },
                new() { Id = 3, Nazev = "TRX",           Datum = DateTime.Today.AddDays(3).AddHours(19), Kapacita = 10, Prihlaseno = 10, JsemPrihlasen = false },
                new() { Id = 4, Nazev = "Jóga (beginner)", Datum = DateTime.Today.AddDays(1).AddHours(16), Kapacita = 20, Prihlaseno = 5,  JsemPrihlasen = false },
            };

            return View(list);
        }
    }
}
