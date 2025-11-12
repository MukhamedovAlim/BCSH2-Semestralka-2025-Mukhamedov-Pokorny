using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Infrastructure.Repositories;
using FitnessCenter.Web.Models;

namespace FitnessCenter.Web.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class EquipmentController : Controller
    {
        private readonly EquipmentRepository _repo;
        public EquipmentController(EquipmentRepository repo) => _repo = repo;

        private static async Task<List<SelectListItem>> LoadFitnessCentersAsync()
        {
            var items = new List<SelectListItem>();
            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand("SELECT idfitness, nazev FROM fitnesscentra ORDER BY nazev", con);
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync()){
                items.Add(new SelectListItem { Value = rd.GetInt32(0).ToString(), Text = rd.GetString(1) });
            }
            return items;
        }
        private async Task FillFitnessViewBagAsync()
        {
            var list = await LoadFitnessCentersAsync();
            ViewBag.FitnessCenters = list;
            ViewBag.Fitka = list;
        }

        // LIST
        [HttpGet("")] // => GET /Equipment
        public async Task<IActionResult> Index(string? typ, int? fitko)
        {
            ViewBag.Filter = string.IsNullOrWhiteSpace(typ) ? "Vše" : typ;
            await FillFitnessViewBagAsync();
            ViewBag.FitkoId = fitko;

            var rows = await _repo.GetAsync(typ, fitko);
            var vm = rows.Select(r => new EquipmentViewModel
            {
                Id = r.Id,
                Nazev = r.Nazev,
                Typ = r.Typ switch { "K" => "Kardio", "P" => "Posilovací", "V" => "Volná závaží", _ => r.Typ },
                Stav = r.Stav,
                Fitko = r.Fitko,
                FitkoId = r.FitkoId
            }).ToList();

            return View(vm);
        }

        // CREATE
        [Authorize(Roles = "Admin")]
        [HttpGet("Create")] // => GET /Equipment/Create
        public async Task<IActionResult> Create()
        {
            await FillFitnessViewBagAsync();
            return View(new EquipmentEditViewModel());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("Create")] // => POST /Equipment/Create
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EquipmentEditViewModel vm)
        {
            // fallback pro Nazev (kdyby binder neposlal)
            var postedNazev = Request.Form["Nazev"].ToString();
            if (string.IsNullOrWhiteSpace(vm.Nazev) && !string.IsNullOrWhiteSpace(postedNazev))
            {
                vm.Nazev = postedNazev;
                ModelState.Clear();
                TryValidateModel(vm);
            }

            if (!ModelState.IsValid)
            {
                var errs = ModelState
                    .Where(kv => kv.Value?.Errors?.Any() == true)
                    .Select(kv => $"{kv.Key}: {string.Join(" | ", kv.Value!.Errors.Select(e => e.ErrorMessage))}");
                TempData["Err"] = "Form error: " + string.Join(" || ", errs);

                await FillFitnessViewBagAsync();
                return View(vm);
            }

            var kdo = User.FindFirst(ClaimTypes.Name)?.Value ?? "Admin";
            var dto = new EquipmentEditDto
            {
                Nazev = vm.Nazev.Trim(),
                Typ = (vm.Typ ?? "K").Trim().ToUpperInvariant(),
                Stav = string.IsNullOrWhiteSpace(vm.Stav) ? "OK" : vm.Stav.Trim(),
                FitkoId = vm.FitkoId
            };

            try
            {
                await _repo.CreateAsync(dto, kdo);
                TempData["Ok"] = "Vybavení bylo přidáno.";
                return RedirectToAction(nameof(Index));
            }
            catch (OracleException ox)
            {
                TempData["Err"] = $"DB chyba {ox.Number}: {ox.Message}";
                await FillFitnessViewBagAsync();
                return View(vm);
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var kdo = User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
            try
            {
                var ok = await _repo.DeleteAsync(id, kdo);
                TempData["Ok"] = ok ? "Vybavení bylo smazáno." : "Záznam nebyl nalezen.";
            }
            catch (InvalidOperationException ex)
            {
                // typicky FK 2292
                TempData["Err"] = ex.Message;
            }
            catch (OracleException ox)
            {
                TempData["Err"] = $"DB chyba {ox.Number}: {ox.Message}";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
