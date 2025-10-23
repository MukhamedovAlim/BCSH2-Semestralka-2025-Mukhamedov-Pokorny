using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Web.Models;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Member,Trainer,Admin")]
    public class EquipmentController : Controller
    {
        public IActionResult Index(string? typ = null)
        {
            ViewBag.Active = "Equipment";
            ViewBag.Filter = typ ?? "Vše";

            var all = new List<EquipmentViewModel>
            {
                new() { Nazev = "Běžecký pás NordicTrack", Typ = "Kardio",       Stav = "OK" },
                new() { Nazev = "Eliptical",               Typ = "Kardio",       Stav = "OK" },
                new() { Nazev = "Bench press",             Typ = "Posilovací",   Stav = "OK" },
                new() { Nazev = "Stroj na dřepy",          Typ = "Posilovací",   Stav = "Servis" },
                new() { Nazev = "Jednoručky 10 kg",        Typ = "Volná závaží", Stav = "OK" },
                new() { Nazev = "Kettlebell 16 kg",        Typ = "Volná závaží", Stav = "OK" },
            };

            var filtered = string.IsNullOrWhiteSpace(typ) || typ == "Vše"
                ? all
                : all.Where(x => x.Typ.Equals(typ, StringComparison.OrdinalIgnoreCase)).ToList();

            return View(filtered);
        }
    }
}
