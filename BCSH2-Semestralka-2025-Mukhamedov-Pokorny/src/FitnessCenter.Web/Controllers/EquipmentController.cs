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
            while (await rd.ReadAsync())
                items.Add(new SelectListItem { Value = rd.GetInt32(0).ToString(), Text = rd.GetString(1) });
            return items;
        }

        private async Task FillFitnessViewBagAsync()
        {
            var list = await LoadFitnessCentersAsync();
            ViewBag.FitnessCenters = list;
            ViewBag.Fitka = list;
        }

        // LIST
        [HttpGet("")]
        public async Task<IActionResult> Index(string? typ, int? fitko)
        {
            // K/P/V nebo null → textový label pro UI
            string filterLabel = typ switch
            {
                "K" => "Kardio",
                "P" => "Posilovací",
                "V" => "Volná závaží",
                _ => "Vše"
            };

            // pokud je tam nějaká blbost, radši filtr vypneme
            if (filterLabel == "Vše")
                typ = null;

            ViewBag.Filter = filterLabel;   // jen text na zobrazení (když budeš chtít)
            ViewBag.SelectedTyp = typ;      // K/P/V/null pro view
            ViewBag.FitkoId = fitko;

            await FillFitnessViewBagAsync();

            var rows = await _repo.GetAsync(typ, fitko);
            var vm = rows.Select(r => new EquipmentViewModel
            {
                Id = r.Id,
                Nazev = r.Nazev,
                Typ = r.Typ switch
                {
                    "K" => "Kardio",
                    "P" => "Posilovací",
                    "V" => "Volná závaží",
                    _ => r.Typ
                },
                Stav = r.Stav,
                Fitko = r.Fitko,
                FitkoId = r.FitkoId
            }).ToList();

            return View(vm);
        }

        // CREATE
        [Authorize(Roles = "Admin")]
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            await FillFitnessViewBagAsync();
            return View(new EquipmentEditViewModel());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EquipmentEditViewModel vm)
        {
            var postedNazev = Request.Form["Nazev"].ToString();
            if (string.IsNullOrWhiteSpace(vm.Nazev) && !string.IsNullOrWhiteSpace(postedNazev))
            {
                vm.Nazev = postedNazev;
                ModelState.Clear();
                TryValidateModel(vm);
            }

            if (!ModelState.IsValid)
            {

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

        // GET /Equipment/Edit/5
        [Authorize(Roles = "Admin")]
        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var dto = await _repo.GetByIdAsync(id);
            if (dto is null) return NotFound();

            await FillFitnessViewBagAsync();

            var vm = new EquipmentEditViewModel
            {
                Id = dto.Id,
                Nazev = dto.Nazev,
                Typ = dto.Typ,
                Stav = dto.Stav,
                FitkoId = dto.FitkoId
            };
            return View(vm);
        }

        // POST /Equipment/Edit/5
        [Authorize(Roles = "Admin")]
        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EquipmentEditViewModel vm)
        {
            if (vm.Id <= 0) vm.Id = id;
            if (id != vm.Id)
                ModelState.AddModelError(nameof(vm.Id), "Nesouhlasí ID v adrese a ve formuláři.");

            if (string.IsNullOrWhiteSpace(vm.Nazev))
                ModelState.AddModelError(nameof(vm.Nazev), "Zadej název.");
            if (vm.FitkoId <= 0)
                ModelState.AddModelError(nameof(vm.FitkoId), "Vyber fitness centrum.");

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var kdo = User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
            var dto = new EquipmentEditDto
            {
                Id = vm.Id,
                Nazev = vm.Nazev.Trim(),
                Typ = (vm.Typ ?? "K").Trim().ToUpperInvariant(),
                Stav = string.IsNullOrWhiteSpace(vm.Stav) ? "OK" : vm.Stav.Trim(),
                FitkoId = vm.FitkoId
            };

            var ok = await _repo.UpdateAsync(dto, kdo);
            if (!ok) return NotFound();

            TempData["Ok"] = "Změny uloženy.";
            return RedirectToAction(nameof(Index));
        }

        // DELETE
        [Authorize(Roles = "Admin")]
        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
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
                TempData["Err"] = ex.Message; // např. navázané záznamy
            }
            catch (OracleException ox)
            {
                TempData["Err"] = $"DB chyba {ox.Number}: {ox.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        // ============================
        //  HIERARCHIE – JEN PRO ADMINA
        // ============================
        [Authorize(Roles = "Admin")]
        [HttpGet("Hierarchy")]
        public async Task<IActionResult> Hierarchy(int? fitkoId)
        {
            // seznam fitek do comboboxu
            var fitka = await _repo.GetFitnessCentersAsync();
            ViewBag.FitnessCenters = fitka;
            ViewBag.SelectedFitnessId = fitkoId;

            // samotná hierarchie (případně filtrovaná)
            var rows = await _repo.GetEquipmentHierarchyAsync(fitkoId);
            return View(rows);
        }
    }
}
