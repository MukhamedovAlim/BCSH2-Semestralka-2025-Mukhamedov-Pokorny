using FitnessCenter.Application.Interfaces;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.Models.Member;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("AdminTrainers")]
    public class AdminTrainersController : Controller
    {
        private readonly IMembersService _members;

        public AdminTrainersController(IMembersService members)
        {
            _members = members;
        }

        // ---- helper: bezpečné čtení INT z Oracle OUT parametru ----
        private static int ReadOutInt(OracleParameter p)
        {
            var v = p?.Value;
            if (v is null) return 0;

            if (v is OracleDecimal od && !od.IsNull)
                return (int)od.Value;

            if (v is decimal dec) return (int)dec;
            if (v is int i) return i;
            if (v is long l) return (int)l;

            return int.TryParse(v.ToString(), out var parsed) ? parsed : 0;
        }

        // malý helper na URL profilovky člena
        private static string BuildProfilePhotoUrl(int memberId)
        {
            var fileName = $"member_{memberId}.jpg";
            var relPath = $"/uploads/avatars/{fileName}";
            var physPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads",
                "avatars",
                fileName);

            return System.IO.File.Exists(physPath) ? relPath : "";
        }

        // ==========================
        //   HLAVNÍ SEZNAM TRENÉRŮ
        //   GET /AdminTrainers
        // ==========================
        [HttpGet("")]
        public async Task<IActionResult> Index(string? search, string? sort)
        {
            ViewBag.Active = "Admin";
            ViewBag.HideMainNav = true;

            ViewBag.Search = search;
            ViewBag.Sort = sort;

            var list = new List<TrainerViewModel>();

            try
            {
                using var con = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand(@"
                    SELECT idtrener, jmeno, prijmeni, email, telefon
                      FROM TRENERI
                     ORDER BY prijmeni, jmeno", (OracleConnection)con);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    list.Add(new TrainerViewModel
                    {
                        TrainerId = rd.GetInt32(0),
                        FirstName = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        LastName = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        Email = rd.IsDBNull(3) ? "" : rd.GetString(3),
                        Phone = rd.IsDBNull(4) ? "" : rd.GetString(4),
                    });
                }
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Nepodařilo se načíst trenéry: " + ex.Message;
            }

            // filtrace podle jména
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                list = list
                    .Where(t => ($"{t.FirstName} {t.LastName}".Trim()).ToLower().Contains(s))
                    .ToList();
            }

            // řazení podle příjmení
            list = sort switch
            {
                "az" => list.OrderBy(t => t.LastName).ThenBy(t => t.FirstName).ToList(),
                "za" => list.OrderByDescending(t => t.LastName).ThenByDescending(t => t.FirstName).ToList(),
                _ => list.OrderBy(t => t.LastName).ThenBy(t => t.FirstName).ToList()
            };

            return View(list); // Views/AdminTrainers/Index.cshtml
        }

        // ==========================
        //   PROMOTE NA TRENÉRA
        // ==========================
        // GET /AdminTrainers/Promote
        [HttpGet("Promote")]
        public async Task<IActionResult> Promote(string? search)
        {
            // jen členové, kteří NEJSOU trenéři
            var members = await _members.GetAllNonTrainersAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                members = members
                    .Where(m =>
                    {
                        var full = $"{m.FirstName} {m.LastName}".Trim();
                        return full.Contains(s, StringComparison.CurrentCultureIgnoreCase);
                    })
                    .ToList();
            }

            ViewBag.Search = search ?? "";
            return View(members);
        }

        // POST /AdminTrainers/Promote
        [HttpPost("Promote")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PromoteMember([FromForm] string email, [FromForm] string? phone)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Err"] = "Chybí e-mail člena.";
                return RedirectToAction(nameof(Promote));
            }

            try
            {
                // pokud má člen telefon v CLENOVE, nepředávej ho (procedura si ho vezme),
                // jinak pošli z formuláře
                var member = (await _members.GetAllAsync())
                             .FirstOrDefault(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase));

                var telefonForProc = string.IsNullOrWhiteSpace(member?.Phone)
                    ? (string.IsNullOrWhiteSpace(phone) ? null : phone.Trim())
                    : null;

                using var con = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand("PROMOTE_TO_TRAINER", (OracleConnection)con)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };

                cmd.Parameters.Add("p_email", OracleDbType.Varchar2, email, ParameterDirection.Input);
                cmd.Parameters.Add("p_telefon", OracleDbType.Varchar2,
                                   (object?)telefonForProc ?? DBNull.Value, ParameterDirection.Input);

                var pOut = new OracleParameter("p_idtrener", OracleDbType.Decimal)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(pOut);

                await cmd.ExecuteNonQueryAsync();

                var newId = ReadOutInt(pOut);
                TempData["Ok"] = newId > 0
                    ? "Člen byl povýšen na trenéra."
                    : "Člen už je trenér – hotovo.";

                return RedirectToAction(nameof(Index));
            }
            catch (OracleException ox)
            {
                string msg;

                // naše custom chyba z procedury – chybí telefon
                if (ox.Number == 20041 || ox.Message.Contains("ORA-20041"))
                {
                    msg = "Chybí telefonní číslo pro trenéra. Doplň ho prosím a zkus to znovu.";
                }
                else
                {
                    msg = "Databázová chyba při povýšení: " + ox.Message;
                }

                TempData["Err"] = msg;
                return RedirectToAction(nameof(Promote));
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Chyba při povýšení: " + ex.Message;
                return RedirectToAction(nameof(Promote));
            }
        }

        // ==========================
        //   EDIT TRENÉRA
        // ==========================
        // GET /AdminTrainers/Edit/5
        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.HideMainNav = true;
            ViewBag.Active = "Admin";

            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(@"
                SELECT idtrener, jmeno, prijmeni, email, telefon
                  FROM TRENERI
                 WHERE idtrener = :id
            ", con)
            { BindByName = true };

            cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;

            using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync())
            {
                TempData["Err"] = "Trenér s daným ID neexistuje.";
                return RedirectToAction(nameof(Index));
            }

            var vm = new TrainerEditViewModel
            {
                TrainerId = rd.GetInt32(0),
                Jmeno = rd.IsDBNull(1) ? null : rd.GetString(1),
                Prijmeni = rd.IsDBNull(2) ? null : rd.GetString(2),
                Email = rd.IsDBNull(3) ? null : rd.GetString(3),
                Telefon = rd.IsDBNull(4) ? null : rd.GetString(4)
            };

            return View(vm); // Views/AdminTrainers/Edit.cshtml
        }

        // POST /AdminTrainers/Edit/5
        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TrainerEditViewModel vm)
        {
            ViewBag.HideMainNav = true;
            ViewBag.Active = "Admin";

            // sladit ID z URL a z formuláře
            if (vm.TrainerId <= 0) vm.TrainerId = id;
            if (vm.TrainerId != id)
                ModelState.AddModelError(nameof(vm.TrainerId), "Nesouhlasí ID v adrese a ve formuláři.");

            // otrimujeme vstupy
            vm.Jmeno = vm.Jmeno?.Trim();
            vm.Prijmeni = vm.Prijmeni?.Trim();
            vm.Email = vm.Email?.Trim();
            vm.Telefon = vm.Telefon?.Trim();

            if (string.IsNullOrWhiteSpace(vm.Jmeno))
                ModelState.AddModelError(nameof(vm.Jmeno), "Zadej jméno.");
            if (string.IsNullOrWhiteSpace(vm.Prijmeni))
                ModelState.AddModelError(nameof(vm.Prijmeni), "Zadej příjmení.");
            if (string.IsNullOrWhiteSpace(vm.Email))
                ModelState.AddModelError(nameof(vm.Email), "Zadej e-mail.");

            if (!ModelState.IsValid)
                return View(vm);

            try
            {
                using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand("PR_TRENER_UPDATE", con)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };

                cmd.Parameters.Add("p_idtrener", OracleDbType.Int32).Value = vm.TrainerId;
                cmd.Parameters.Add("p_jmeno", OracleDbType.Varchar2).Value =
                    (object?)vm.Jmeno ?? DBNull.Value;
                cmd.Parameters.Add("p_prijmeni", OracleDbType.Varchar2).Value =
                    (object?)vm.Prijmeni ?? DBNull.Value;
                cmd.Parameters.Add("p_telefon", OracleDbType.Varchar2).Value =
                    string.IsNullOrWhiteSpace(vm.Telefon) ? (object)DBNull.Value : vm.Telefon;
                cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value =
                    (object?)vm.Email ?? DBNull.Value;

                await cmd.ExecuteNonQueryAsync();

                TempData["Ok"] = "Trenér byl úspěšně upraven.";
                return RedirectToAction(nameof(Index));
            }
            catch (OracleException ox) when (ox.Number == 20051)
            {
                TempData["Err"] = "Trenér s daným ID neexistuje.";
                return RedirectToAction(nameof(Index));
            }
            catch (OracleException ox) when (ox.Number == 20052)
            {
                ModelState.AddModelError(string.Empty, "Telefon nebo e-mail je již použit u jiného trenéra.");
                return View(vm);
            }
            catch (OracleException ox)
            {
                ModelState.AddModelError(string.Empty, "Chyba DB: " + ox.Message);
                return View(vm);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Neočekávaná chyba: " + ex.Message);
                return View(vm);
            }
        }

        // ==========================
        //   Smazání / DEMOTE
        // ==========================
        // POST /AdminTrainers/Delete
        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromForm] int id)
        {
            if (id <= 0)
            {
                TempData["Err"] = "Neplatné ID trenéra.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

                // 1) Zkus FORCE variantu (s počty smazaných věcí)
                try
                {
                    using var cmd = new OracleCommand("DEMOTE_TRAINER_FORCE", con)
                    {
                        CommandType = CommandType.StoredProcedure,
                        BindByName = true
                    };
                    cmd.Parameters.Add("p_idtrener", OracleDbType.Int32).Value = id;

                    var pLek = new OracleParameter("p_del_lekci", OracleDbType.Decimal) { Direction = ParameterDirection.Output };
                    var pVaz = new OracleParameter("p_del_vazeb", OracleDbType.Decimal) { Direction = ParameterDirection.Output };
                    var pRez = new OracleParameter("p_del_rez", OracleDbType.Decimal) { Direction = ParameterDirection.Output };

                    cmd.Parameters.Add(pLek);
                    cmd.Parameters.Add(pVaz);
                    cmd.Parameters.Add(pRez);

                    await cmd.ExecuteNonQueryAsync();

                    int zLek = ReadOutInt(pLek);
                    int zVaz = ReadOutInt(pVaz);
                    int zRez = ReadOutInt(pRez);

                    var parts = new List<string>();
                    if (zLek > 0) parts.Add($"{zLek} zrušených lekcí");
                    if (zVaz > 0) parts.Add($"{zVaz} odstraněných vazeb");
                    if (zRez > 0) parts.Add($"{zRez} smazaných rezervací");
                    var tail = parts.Count > 0 ? " (" + string.Join(", ", parts) + ")" : "";

                    TempData["Ok"] = "Trenér byl zrušen (ponechán jako člen)" + tail + ".";
                    return RedirectToAction(nameof(Index));
                }
                catch (OracleException ox) when (
                       ox.Number == 6550   // PLS-00302/PLS-00201… – kompilační/param chyby
                    || ox.Number == 4043   // ORA-04043: objekt neexistuje
                    || ox.Number == 6553)  // jiné PL/SQL chyby, procedura není k dispozici
                {
                    // spadne do fallbacku
                }

                // 2) Fallback – původní DEMOTE_TRAINER (selže, pokud má lekce)
                using (var cmd2 = new OracleCommand("DEMOTE_TRAINER", con)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                })
                {
                    cmd2.Parameters.Add("p_idtrener", OracleDbType.Int32).Value = id;
                    await cmd2.ExecuteNonQueryAsync();
                    TempData["Ok"] = "Trenér byl zrušen (ponechán jako člen).";
                }
            }
            catch (OracleException ox) when (ox.Number == 20045)
            {
                TempData["Err"] = "Nelze zrušit trenéra: má přiřazené lekce. Použij variantu FORCE (DEMOTE_TRAINER_FORCE).";
            }
            catch (OracleException ox)
            {
                TempData["Err"] = $"Chyba DB při rušení trenéra: {ox.Message}";
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Chyba při rušení trenéra: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================
        //   ADMIN PREVIEW TRENÉRŮ
        //   GET /AdminTrainers/Preview
        // ==========================
        [HttpGet("Preview")]
        public async Task<IActionResult> AdminPreview()
        {
            ViewBag.HideMainNav = true;
            ViewBag.Active = "Admin";

            var list = new List<MemberTrainerListItem>();

            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(@"
                SELECT idtrener,
                       jmeno,
                       prijmeni,
                       telefon
                  FROM treneri
                 ORDER BY prijmeni, jmeno", con);

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new MemberTrainerListItem
                {
                    Id = rd.GetInt32(0),
                    Jmeno = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Prijmeni = rd.IsDBNull(2) ? "" : rd.GetString(2),
                    Telefon = rd.IsDBNull(3) ? "" : rd.GetString(3)
                });
            }

            return View("AdminPreview", list); // Views/AdminTrainers/AdminPreview.cshtml
        }

        // ===========================
        //  ADMIN PREVIEW DETAIL TRENÉRA
        //  GET /AdminTrainers/AdminPreviewDetails/5
        // ===========================
        [HttpGet("AdminPreviewDetails/{id:int}")]
        public async Task<IActionResult> AdminPreviewDetails(int id)
        {
            var model = new MemberTrainerDetailViewModel();

            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

            // 1) základní info o trenérovi
            using (var cmd = new OracleCommand(@"
                SELECT idtrener,
                       jmeno,
                       prijmeni,
                       email,
                       telefon
                  FROM treneri
                 WHERE idtrener = :id", con)
            { BindByName = true })
            {
                cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;

                using var rd = await cmd.ExecuteReaderAsync();
                if (!await rd.ReadAsync())
                {
                    TempData["Err"] = "Trenér s daným ID neexistuje.";
                    return RedirectToAction(nameof(AdminPreview));
                }

                model.TrenerId = rd.GetInt32(0);
                model.Jmeno = rd.IsDBNull(1) ? "" : rd.GetString(1);
                model.Prijmeni = rd.IsDBNull(2) ? "" : rd.GetString(2);
                model.Email = rd.IsDBNull(3) ? "" : rd.GetString(3);
                model.Telefon = rd.IsDBNull(4) ? "" : rd.GetString(4);
            }

            // 1b) zkus najít odpovídajícího člena podle e-mailu trenéra,
            // abychom mohli vzít jeho profilovku
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                using var cmdMember = new OracleCommand(@"
                    SELECT idclen
                      FROM clenove
                     WHERE LOWER(email) = LOWER(:mail)", con)
                { BindByName = true };

                cmdMember.Parameters.Add("mail", OracleDbType.Varchar2).Value = model.Email;

                var result = await cmdMember.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    var memberId = Convert.ToInt32(result);
                    model.MemberId = memberId;
                    model.ProfilePhotoUrl = BuildProfilePhotoUrl(memberId);
                }
            }

            // 2) nadcházející lekce trenéra
            model.Lekce = new List<MemberTrainerLessonRow>();

            using (var cmd2 = new OracleCommand(@"
                SELECT idlekce,
                       nazevlekce,
                       datumlekce,
                       obsazenost
                  FROM lekce
                 WHERE trener_idtrener = :id
                   AND datumlekce >= TRUNC(SYSDATE)
                 ORDER BY datumlekce", con)
            { BindByName = true })
            {
                cmd2.Parameters.Add("id", OracleDbType.Int32).Value = id;

                using var rd2 = await cmd2.ExecuteReaderAsync();
                while (await rd2.ReadAsync())
                {
                    model.Lekce.Add(new MemberTrainerLessonRow
                    {
                        IdLekce = rd2.GetInt32(0),
                        Nazev = rd2.GetString(1),
                        Datum = rd2.GetDateTime(2),
                        Obsazenost = rd2.GetInt32(3)
                    });
                }
            }

            model.PocetLekci = model.Lekce.Count;

            return View("AdminPreviewDetails", model);
        }
    }
}
