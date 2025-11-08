using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.Models.Member; // MemberViewModel
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;
using System.Data;
using MemberVM = FitnessCenter.Web.Models.Member.MemberViewModel;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Members")]
    public class MembersController : Controller
    {
        private readonly IMembersService _members;

        public MembersController(IMembersService members) => _members = members;

        // ---------- helper: číselník fitness center ----------
        private static async Task<List<SelectListItem>> LoadFitnessForSelectAsync()
        {
            var items = new List<SelectListItem>();
            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(
                "SELECT idfitness, nazev FROM fitnesscentra ORDER BY nazev",
                (OracleConnection)conn);

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                items.Add(new SelectListItem
                {
                    Value = rd.GetInt32(0).ToString(),
                    Text = rd.GetString(1)
                });
            }
            return items;
        }

        // ===== LIST: GET /Members =====
        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewBag.Active = "Members";
            ViewBag.HideMainNav = true;

            var list = new System.Collections.Generic.List<MemberVM>();

            try
            {
                using var conn = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand(@"
                    SELECT
                        idclen,
                        jmeno,
                        prijmeni,
                        email,
                        telefon,
                        datumnarozeni,
                        adresa
                    FROM CLENOVE
                    ORDER BY prijmeni, jmeno", (OracleConnection)conn);

                using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    list.Add(new MemberVM
                    {
                        MemberId = rd.GetInt32(0),
                        FirstName = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        LastName = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        Email = rd.IsDBNull(3) ? null : rd.GetString(3),
                        Phone = rd.IsDBNull(4) ? null : rd.GetString(4),
                        BirthDate = rd.IsDBNull(5) ? (DateTime?)null : rd.GetDateTime(5),
                        Address = rd.IsDBNull(6) ? null : rd.GetString(6),
                        IsActive = true
                    });

                }
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Nepodařilo se načíst seznam členů: " + ex.Message;
                list = new List<MemberViewModel>();
            }

            return View(list);
        }

        // ===== CREATE (GET): /Members/Create =====
        // GET: /Members/Create
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
            return View(new MemberCreateViewModel());
        }

        // ===== CREATE (POST): /Members/Create =====
        // POST: /Members/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MemberCreateViewModel vm)
        {
            // vždy znovu naplníme dropdown
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();

            // tvrdé validace – důvod, proč se to často „neuloží“
            if (!vm.BirthDate.HasValue)
                ModelState.AddModelError(nameof(vm.BirthDate), "Zadej datum narození.");

            if (vm.FitnessCenterId <= 0)
                ModelState.AddModelError(nameof(vm.FitnessCenterId), "Vyber fitness centrum.");

            // Když je něco špatně, ukaž přesný důvod
            if (!ModelState.IsValid)
            {
                var errs = string.Join("; ", ModelState
                    .Where(kv => kv.Value?.Errors.Count > 0)
                    .SelectMany(kv => kv.Value!.Errors.Select(e => $"{kv.Key}: {e.ErrorMessage}")));
                if (!string.IsNullOrWhiteSpace(errs))
                    TempData["Err"] = "Neplatný formulář: " + errs;

                return View(vm);
            }

            // pojistka: existuje zvolené fitko?
            using (var conn = await DatabaseManager.GetOpenConnectionAsync())
            using (var chk = new Oracle.ManagedDataAccess.Client.OracleCommand(
                       "SELECT COUNT(*) FROM fitnesscentra WHERE idfitness=:id",
                       (Oracle.ManagedDataAccess.Client.OracleConnection)conn))
            {
                chk.BindByName = true;
                chk.Parameters.Add("id", vm.FitnessCenterId);
                var exists = Convert.ToInt32(await chk.ExecuteScalarAsync()) > 0;
                if (!exists)
                {
                    ModelState.AddModelError(nameof(vm.FitnessCenterId), "Zvolené fitness centrum neexistuje.");
                    TempData["Err"] = "Zvolené fitness centrum neexistuje.";
                    return View(vm);
                }
            }

            var m = new FitnessCenter.Domain.Entities.Member
            {
                FirstName = vm.FirstName?.Trim() ?? "",
                LastName = vm.LastName?.Trim() ?? "",
                Email = vm.Email?.Trim() ?? "",
                Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim(),
                Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim(),
                BirthDate = vm.BirthDate!.Value,
                FitnessCenterId = vm.FitnessCenterId
            };

            try
            {
                var newId = await _members.CreateViaProcedureAsync(m); // volá PR_CLEN_CREATE
                TempData["Ok"] = $"Člen byl vytvořen (ID: {newId}).";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // sem spadne např. kolize unikátního emailu/telefonu
                TempData["Err"] = "Chyba při vytváření člena: " + ex.Message;
                return View(vm);
            }
        }

        // ===== DELETE (POST): /Members/Delete =====
        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromForm] int id)
        {
            if (id <= 0)
            {
                TempData["Err"] = "Neplatné ID člena.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
                using var tx = con.BeginTransaction();

                // závislosti
                try
                {
                    using var cmdRez = new OracleCommand(
                        "DELETE FROM REZERVACE_LEKCI WHERE CLEN_IDCLEN = :id", con)
                    { BindByName = true, Transaction = tx };
                    cmdRez.Parameters.Add("id", OracleDbType.Int32, id, ParameterDirection.Input);
                    await cmdRez.ExecuteNonQueryAsync();
                }
                catch (OracleException ox) when (ox.Number == 942) { }

                try
                {
                    using var cmdPlatby = new OracleCommand(
                        "DELETE FROM PLATBY WHERE CLEN_IDCLEN = :id", con)
                    { BindByName = true, Transaction = tx };
                    cmdPlatby.Parameters.Add("id", OracleDbType.Int32, id, ParameterDirection.Input);
                    await cmdPlatby.ExecuteNonQueryAsync();
                }
                catch (OracleException ox) when (ox.Number == 942) { }

                try
                {
                    using var cmdClenstvi = new OracleCommand(
                        "DELETE FROM CLENSTVI WHERE CLEN_IDCLEN = :id", con)
                    { BindByName = true, Transaction = tx };
                    cmdClenstvi.Parameters.Add("id", OracleDbType.Int32, id, ParameterDirection.Input);
                    await cmdClenstvi.ExecuteNonQueryAsync();
                }
                catch (OracleException ox) when (ox.Number == 942) { }

                // samotný člen
                using (var cmdClen = new OracleCommand(
                    "DELETE FROM CLENOVE WHERE IDCLEN = :id", con))
                {
                    cmdClen.BindByName = true;
                    cmdClen.Transaction = tx;
                    cmdClen.Parameters.Add("id", OracleDbType.Int32, id, ParameterDirection.Input);

                    var rows = await cmdClen.ExecuteNonQueryAsync();
                    if (rows == 0)
                    {
                        tx.Rollback();
                        TempData["Err"] = "Člen nebyl nalezen.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                tx.Commit();
                TempData["Ok"] = "Člen byl smazán.";
            }
            catch (OracleException ox)
            {
                TempData["Err"] = "Chyba DB při mazání člena: " + ox.Message;
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Chyba při mazání člena: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
