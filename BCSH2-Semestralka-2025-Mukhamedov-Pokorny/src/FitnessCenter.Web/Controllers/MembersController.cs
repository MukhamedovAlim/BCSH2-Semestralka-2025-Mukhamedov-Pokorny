using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Infrastructure.Security;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.Models.Member;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Text;
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
            ViewBag.Search = search;
            ViewBag.Sort = sort;

            var list = new List<MemberVM>();

            try
            {
                using var conn = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand(@"
SELECT
    c.idclen,
    c.jmeno,
    c.prijmeni,
    c.email,
    c.telefon,
    c.datumnarozeni,
    c.adresa,
    c.fitnesscentrum_idfitness,
    f.nazev AS fitko,

    -- poslední členství (1 řádek na člena)
    m.zahajeni,
    m.ukonceni,

    -- trenér?
    CASE WHEN t.idtrener IS NOT NULL THEN 1 ELSE 0 END AS is_trainer
FROM clenove c
LEFT JOIN fitnesscentra f ON f.idfitness = c.fitnesscentrum_idfitness
LEFT JOIN (
    SELECT z.clen_idclen, z.zahajeni, z.ukonceni
    FROM (
        SELECT cl.*,
               ROW_NUMBER() OVER(
                   PARTITION BY cl.clen_idclen
                   ORDER BY cl.zahajeni DESC NULLS LAST
               ) rn
        FROM clenstvi cl
    ) z
    WHERE z.rn = 1
) m ON m.clen_idclen = c.idclen
LEFT JOIN treneri t ON LOWER(t.email) = LOWER(c.email)
ORDER BY c.prijmeni, c.jmeno
", (OracleConnection)conn);

                using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    list.Add(new MemberVM
                    {
                        MemberId = rd.GetInt32(0),
                        FirstName = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        LastName = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        Email = rd.IsDBNull(3) ? "" : rd.GetString(3),
                        Phone = rd.IsDBNull(4) ? null : rd.GetString(4),
                        BirthDate = rd.IsDBNull(5) ? (DateTime?)null : rd.GetDateTime(5),
                        Address = rd.IsDBNull(6) ? null : rd.GetString(6),

                        FitnessId = rd.IsDBNull(7) ? 0 : rd.GetInt32(7),
                        FitnessName = rd.IsDBNull(8) ? "" : rd.GetString(8),

                        MembershipFrom = rd.IsDBNull(9) ? (DateTime?)null : rd.GetDateTime(9),
                        MembershipTo = rd.IsDBNull(10) ? (DateTime?)null : rd.GetDateTime(10),

                        IsTrainer = !rd.IsDBNull(11) && rd.GetInt32(11) == 1
                    });
                }
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Nepodařilo se načíst seznam členů: " + ex.Message;
                list = new List<MemberVM>();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                list = list.Where(m => ($"{m.FirstName} {m.LastName}").ToLower().Contains(s)).ToList();
            }

            list = sort switch
            {
                "az" => list.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToList(),
                "za" => list.OrderByDescending(m => m.LastName).ThenByDescending(m => m.FirstName).ToList(),
                _ => list.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToList()
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

        [HttpPost("/Members/Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete([FromForm] int id)
        {
            if (id <= 0)
            {
                TempData["Err"] = "Neplatné ID člena.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
                using var tx = con.BeginTransaction();

                // 1) NAČTI DETAILY PRO LIDSKÝ VÝSTUP (bez ID)
                var rezList = new List<string>();
                var payList = new List<string>();
                var memList = new List<string>();

                // Rezervace (zkusíme view v_clen_rezervace; fallback na joiny)
                try
                {
                    using var cmdRez = new OracleCommand(@"
                SELECT nazevlekce, datumlekce
                FROM v_clen_rezervace
                WHERE id_clena = :id
                ORDER BY datumlekce", con)
                    { BindByName = true, Transaction = tx };
                    cmdRez.Parameters.Add("id", OracleDbType.Int32).Value = id;

                    using var rd = await cmdRez.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        var nazev = rd.IsDBNull(0) ? "Lekce" : rd.GetString(0);
                        var dt = rd.IsDBNull(1) ? (DateTime?)null : rd.GetDateTime(1);
                        rezList.Add(dt.HasValue
                            ? $"{nazev} – {dt.Value:dd.MM.yyyy HH:mm}"
                            : nazev);
                    }
                }
                catch (OracleException ox) when (ox.Number == 942) // view neexistuje
                {
                    using var cmdRez2 = new OracleCommand(@"
                SELECT l.nazevlekce, l.datumlekce
                FROM   REZERVACELEKCI r
                JOIN   RELEKCI rl ON rl.REZERVACELEKCI_IDREZERVACE = r.IDREZERVACE
                                  AND rl.REZERVACELEKCI_CLEN_IDCLEN = r.CLEN_IDCLEN
                JOIN   LEKCE l    ON l.IDLEKCE = rl.LEKCE_IDLEKCE
                WHERE  r.CLEN_IDCLEN = :id
                ORDER  BY l.DATUMLEKCE", con)
                    { BindByName = true, Transaction = tx };
                    cmdRez2.Parameters.Add("id", OracleDbType.Int32).Value = id;

                    using var rd = await cmdRez2.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        var nazev = rd.IsDBNull(0) ? "Lekce" : rd.GetString(0);
                        var dt = rd.IsDBNull(1) ? (DateTime?)null : rd.GetDateTime(1);
                        rezList.Add(dt.HasValue
                            ? $"{nazev} – {dt.Value:dd.MM.yyyy HH:mm}"
                            : nazev);
                    }
                }

                // Platby (částka + stav + datum)
                using (var cmdPay = new OracleCommand(@"
            SELECT p.CASTKA, NVL(s.STAVPLATBY,'(neznámý)'), p.DATUMPLATBY
            FROM   PLATBY p
            LEFT   JOIN STAVYPLATEB s ON s.IDSTAVPLATBY = p.STAVPLATBY_IDSTAVPLATBY
            WHERE  p.CLEN_IDCLEN = :id
            ORDER  BY p.DATUMPLATBY", con)
                { BindByName = true, Transaction = tx })
                {
                    cmdPay.Parameters.Add("id", OracleDbType.Int32).Value = id;
                    using var rd = await cmdPay.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        var castka = rd.IsDBNull(0) ? 0m : rd.GetDecimal(0);
                        var stav = rd.GetString(1);
                        var dt = rd.IsDBNull(2) ? (DateTime?)null : rd.GetDateTime(2);
                        payList.Add(dt.HasValue
                            ? $"{castka:0} Kč – {stav} – {dt.Value:dd.MM.yyyy}"
                            : $"{castka:0} Kč – {stav}");
                    }
                }

                // Členství (typ + období)
                using (var cmdMem = new OracleCommand(@"
            SELECT NVL(tc.NAZEV,'(typ neuveden)'), cl.ZAHAJENI, cl.UKONCENI
            FROM   CLENSTVI cl
            LEFT   JOIN TYPYCLENSTVI tc ON tc.IDTYPCLENSTVI = cl.TYPCLENSTVI_IDTYPCLENSTVI
            WHERE  cl.CLEN_IDCLEN = :id
            ORDER  BY cl.ZAHAJENI", con)
                { BindByName = true, Transaction = tx })
                {
                    cmdMem.Parameters.Add("id", OracleDbType.Int32).Value = id;
                    using var rd = await cmdMem.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        var typ = rd.GetString(0);
                        var od = rd.IsDBNull(1) ? (DateTime?)null : rd.GetDateTime(1);
                        var @do = rd.IsDBNull(2) ? (DateTime?)null : rd.GetDateTime(2);
                        if (od.HasValue && @do.HasValue)
                            memList.Add($"{typ}: {od.Value:dd.MM.yyyy} – {@do.Value:dd.MM.yyyy}");
                        else
                            memList.Add(typ);
                    }
                }

                // 2) SMAZÁNÍ (od dětí k rodiči)
                static async Task<int> ExecDelAsync(OracleConnection c, OracleTransaction t, string sql, int memberId)
                {
                    using var cmd = new OracleCommand(sql, c) { BindByName = true, Transaction = t };
                    cmd.Parameters.Add("id", OracleDbType.Int32).Value = memberId;
                    return await cmd.ExecuteNonQueryAsync();
                }

                // a) vazby lekce–rezervace daného člena
                var delRelekci = await ExecDelAsync(con, tx, @"
            DELETE FROM RELEKCI rl
            WHERE EXISTS (
              SELECT 1
                FROM REZERVACELEKCI r
               WHERE r.IDREZERVACE = rl.REZERVACELEKCI_IDREZERVACE
                 AND r.CLEN_IDCLEN  = :id
            )", id);

                // b) rezervace
                var delRez = await ExecDelAsync(con, tx,
                    "DELETE FROM REZERVACELEKCI WHERE CLEN_IDCLEN = :id", id);

                // c) členství
                var delClenstvi = await ExecDelAsync(con, tx,
                    "DELETE FROM CLENSTVI WHERE CLEN_IDCLEN = :id", id);

                // d) platby
                var delPlatby = await ExecDelAsync(con, tx,
                    "DELETE FROM PLATBY WHERE CLEN_IDCLEN = :id", id);

                // e) dokumenty – pokud je používáš pro členy (nepovinné)
                int delDok = 0;
                try
                {
                    delDok = await ExecDelAsync(con, tx,
                        "DELETE FROM DOKUMENTY WHERE CLEN_IDCLEN = :id", id);
                }
                catch (OracleException ox) when (ox.Number == 942) { /* tabulka neexistuje – OK */ }

                // f) samotný člen
                int delClen;
                using (var cmdMember = new OracleCommand("DELETE FROM CLENOVE WHERE IDCLEN = :id", con)
                { BindByName = true, Transaction = tx })
                {
                    cmdMember.Parameters.Add("id", OracleDbType.Int32).Value = id;
                    delClen = await cmdMember.ExecuteNonQueryAsync();
                }

                if (delClen == 0)
                {
                    tx.Rollback();
                    TempData["Err"] = "Člen nebyl nalezen nebo už byl smazán.";
                    return RedirectToAction(nameof(Index));
                }

                tx.Commit();

                // 3) LIDSKÁ ZPRÁVA – bez ID, jen popisy
                string JoinPreview(IEnumerable<string> list, int max = 5, string emptyText = "žádné")
                {
                    var arr = list.Take(max).ToList();
                    if (arr.Count == 0) return emptyText;
                    var more = list.Count() - arr.Count;
                    return more > 0 ? $"{string.Join(", ", arr)} a další {more}…" : string.Join(", ", arr);
                }

                var sb = new StringBuilder();
                if (rezList.Any()) sb.AppendLine($"Rezervace: {JoinPreview(rezList)}.");
                if (payList.Any()) sb.AppendLine($"Platby: {JoinPreview(payList)}.");
                if (memList.Any()) sb.AppendLine($"Členství: {JoinPreview(memList)}.");
                if (delDok > 0) sb.AppendLine($"Dokumenty: {delDok} souborů smazáno.");

                // kdyby byl uživatel „čistý“ bez záznamů
                if (sb.Length == 0) sb.Append("Uživatel neměl žádné rezervace, platby ani členství.");

                TempData["Ok"] = $"Člen byl smazán. {sb}";
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
