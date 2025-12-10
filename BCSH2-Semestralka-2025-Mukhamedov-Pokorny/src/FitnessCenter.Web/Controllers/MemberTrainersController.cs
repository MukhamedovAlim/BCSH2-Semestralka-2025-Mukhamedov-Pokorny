using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models.Member;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Member")]
    [Route("MemberTrainers")]
    public sealed class MemberTrainersController : Controller
    {
        // ==========================
        //   pomocná metoda pro avatar
        // ==========================
        private static string? BuildProfilePhotoUrl(int memberId)
        {
            var fileName = $"member_{memberId}.jpg";
            var relative = $"/uploads/avatars/{fileName}";

            var path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads",
                "avatars",
                fileName);

            return System.IO.File.Exists(path) ? relative : null;
        }

        // GET /MemberTrainers
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var list = new List<MemberTrainerListItem>();

            try
            {
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
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Nepodařilo se načíst trenéry: " + ex.Message;
            }

            return View(list); // Views/MemberTrainers/Index.cshtml
        }

        // GET /MemberTrainers/Detail/5
        [HttpGet("Detail/{id:int}")]
        public async Task<IActionResult> Detail(int id)
        {
            var model = new MemberTrainerDetailViewModel();

            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

            // 1) Trenér
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
                    return RedirectToAction(nameof(Index));
                }

                model.TrenerId = rd.GetInt32(0);
                model.Jmeno = rd.IsDBNull(1) ? "" : rd.GetString(1);
                model.Prijmeni = rd.IsDBNull(2) ? "" : rd.GetString(2);
                model.Email = rd.IsDBNull(3) ? "" : rd.GetString(3);
                model.Telefon = rd.IsDBNull(4) ? "" : rd.GetString(4);
            }

            // 1b) pokusit se najít odpovídajícího člena podle e-mailu trenéra
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

            // 2) Lekce trenéra (pouze nadcházející)
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

            // 3) počet lekcí vezmeme z listu
            model.PocetLekci = model.Lekce.Count;

            return View(model); // Views/MemberTrainers/Detail.cshtml
        }
    }
}
