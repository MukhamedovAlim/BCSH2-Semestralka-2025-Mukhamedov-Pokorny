using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.Models.Member;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using FitnessCenter.Web.Infrastructure.Security;
using MemberVM = FitnessCenter.Web.Models.Member.MemberViewModel;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MembersController : Controller
    {
        private readonly IMembersService _members;

        public MembersController(IMembersService members)
        {
            _members = members;
        }

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

        // ===== PING: rychlá kontrola routování =====
        // GET /Members/Ping  -> "OK"
        [HttpGet("/Members/Ping")]
        public IActionResult Ping() => Content("OK");

        // ===== LIST =====
        // GET /Members        (explicitně)
        // GET /Members/Index  (explicitně)
        [HttpGet("/Members")]
        [HttpGet("/Members/Index")]
        public async Task<IActionResult> Index(string? search, string? sort, CancellationToken ct)
        {
            ViewBag.Active = "Members";
            ViewBag.HideMainNav = true;

            // aby to šlo předvyplnit ve view
            ViewBag.Search = search;
            ViewBag.Sort = sort;

            var list = new List<MemberVM>();

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

            // 🔎 Filtrování podle jména (Jméno + Příjmení)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                list = list
                    .Where(m => ($"{m.FirstName} {m.LastName}").ToLower().Contains(s))
                    .ToList();
            }

            // 🔢 Řazení podle příjmení (A→Z / Z→A)
            list = sort switch
            {
                "az" => list
                    .OrderBy(m => m.LastName)
                    .ThenBy(m => m.FirstName)
                    .ToList(),
                "za" => list
                    .OrderByDescending(m => m.LastName)
                    .ThenByDescending(m => m.FirstName)
                    .ToList(),
                _ => list
                    .OrderBy(m => m.LastName)
                    .ThenBy(m => m.FirstName)
                    .ToList()
            };

            return View(list);
        }

        // ===== CREATE (GET): /Members/Create =====
        [HttpGet("/Members/Create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
            return View(new MemberCreateViewModel());
        }

        // ===== CREATE (POST): /Members/Create =====
        [HttpPost("/Members/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MemberCreateViewModel vm)
        {
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();

            if (!vm.BirthDate.HasValue)
                ModelState.AddModelError(nameof(vm.BirthDate), "Zadej datum narození.");

            if (vm.FitnessCenterId <= 0)
                ModelState.AddModelError(nameof(vm.FitnessCenterId), "Vyber fitness centrum.");

            if (!ModelState.IsValid)
            {
                var errs = string.Join("; ", ModelState
                    .Where(kv => kv.Value?.Errors.Count > 0)
                    .SelectMany(kv => kv.Value!.Errors.Select(e => $"{kv.Key}: {e.ErrorMessage}")));
                if (!string.IsNullOrWhiteSpace(errs))
                    TempData["Err"] = "Neplatný formulář: " + errs;

                return View(vm);
            }

            // existence fitka
            using (var conn = await DatabaseManager.GetOpenConnectionAsync())
            using (var chk = new OracleCommand(
                       "SELECT COUNT(*) FROM fitnesscentra WHERE idfitness=:id",
                       (OracleConnection)conn))
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

            var m = new Member
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
                await _members.CreateViaProcedureAsync(m);
                TempData["Ok"] = "Člen byl vytvořen.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Chyba při vytváření člena: " + ex.Message;
                return View(vm);
            }
        }

        // ===== DELETE (POST): /Members/Delete =====
        [HttpPost("/Members/Delete")]
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
                var deleteSql = new[]
                {
                    "DELETE FROM REZERVACE_LEKCI WHERE CLEN_IDCLEN = :id",
                    "DELETE FROM PLATBY WHERE CLEN_IDCLEN = :id",
                    "DELETE FROM CLENSTVI WHERE CLEN_IDCLEN = :id"
                };

                foreach (var sql in deleteSql)
                {
                    try
                    {
                        using var cmd = new OracleCommand(sql, con)
                        { BindByName = true, Transaction = tx };
                        cmd.Parameters.Add("id", OracleDbType.Int32, id, ParameterDirection.Input);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch (OracleException ox) when (ox.Number == 942) { }
                }

                using var cmdMember = new OracleCommand(
                    "DELETE FROM CLENOVE WHERE IDCLEN = :id", con)
                { BindByName = true, Transaction = tx };
                cmdMember.Parameters.Add("id", OracleDbType.Int32, id, ParameterDirection.Input);

                var rows = await cmdMember.ExecuteNonQueryAsync();
                if (rows == 0)
                {
                    tx.Rollback();
                    TempData["Err"] = "Člen nebyl nalezen.";
                    return RedirectToAction(nameof(Index));
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

        // ===== MŮJ PROFIL: /Profile =====
        [Authorize]                               // ať funguje i v emulaci
        [HttpGet("/Profile")]
        public async Task<IActionResult> Profile(CancellationToken ct)
        {
            // ID aktuálního (nebo emulovaného) uživatele
            var memberId = User.GetRequiredCurrentMemberId();

            // načti člena
            var me = await _members.GetByIdAsync(memberId);
            if (me == null) return NotFound();

            // (volitelné) aktivní permanentka
            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(@"
        SELECT IDCLENSTVI, TYP, DAT_OD, DAT_DO
        FROM CLENSTVI
        WHERE CLEN_IDCLEN = :id
          AND DAT_OD <= SYSDATE
          AND (DAT_DO IS NULL OR DAT_DO >= SYSDATE)
        ORDER BY DAT_OD DESC",
                (OracleConnection)conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32, memberId);

            using var rd = await cmd.ExecuteReaderAsync(ct);
            object? membership = null;
            if (await rd.ReadAsync(ct))
            {
                membership = new
                {
                    Id = rd.GetInt32(0),
                    Typ = rd.GetString(1),
                    Od = rd.GetDateTime(2),
                    Do = rd.IsDBNull(3) ? (DateTime?)null : rd.GetDateTime(3)
                };
            }

            ViewBag.Membership = membership;
            return View("Profile", me); // view: Views/Members/Profile.cshtml
        }

    }
}
