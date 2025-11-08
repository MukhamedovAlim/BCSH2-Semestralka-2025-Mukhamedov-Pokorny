using System.Data;
using FitnessCenter.Application.Interfaces;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Linq;

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

        // GET /AdminTrainers
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {

            ViewBag.Active = "Admin";
            ViewBag.HideMainNav = true;

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

            return View(list); // /Views/AdminTrainers/Index.cshtml
        }

        // GET /AdminTrainers/Promote
        [HttpGet("Promote")]
        public async Task<IActionResult> Promote()
        {
            ViewBag.Active = "Admin";
            ViewBag.HideMainNav = true;

            var members = await _members.GetAllAsync();

            // odfiltruj už povýšené (jsou v TRENERI)
            HashSet<string> trainerEmails = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var con = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand("SELECT email FROM TRENERI", (OracleConnection)con);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    if (!rd.IsDBNull(0)) trainerEmails.Add(rd.GetString(0));
            }
            catch { /* necháme bez pádu */ }

            var onlyNotTrainers = members
                .Where(m => !string.IsNullOrWhiteSpace(m.Email) && !trainerEmails.Contains(m.Email!))
                .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
                .ToList();

            return View("~/Views/AdminTrainers/Promote.cshtml", onlyNotTrainers);
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
                // varianta A – když má člen telefon, vezme se z CLENOVE; jinak použij vstup
                var member = (await _members.GetAllAsync())
                             .FirstOrDefault(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase));
                var memberHasPhone = !string.IsNullOrWhiteSpace(member?.Phone);
                var telefonForProc = memberHasPhone ? null : (string.IsNullOrWhiteSpace(phone) ? null : phone.Trim());

                using var con = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand("PROMOTE_TO_TRAINER", (OracleConnection)con)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };
                cmd.Parameters.Add("p_email", OracleDbType.Varchar2, email, ParameterDirection.Input);
                cmd.Parameters.Add("p_telefon", OracleDbType.Varchar2,
                                   (object?)telefonForProc ?? DBNull.Value, ParameterDirection.Input);

                var outId = new OracleParameter("p_idtrener", OracleDbType.Int32)
                { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(outId);

                await cmd.ExecuteNonQueryAsync();

                var newId = outId.Value == null ? 0 : Convert.ToInt32(outId.Value.ToString());
                TempData["Ok"] = newId > 0
                    ? $"Člen byl povýšen na trenéra (ID {newId})."
                    : "Člen už je trenér – hotovo.";

                return RedirectToAction(nameof(Index));
            }
            catch (OracleException ox)
            {
                TempData["Err"] = $"DB chyba při povýšení: {ox.Message}";
                return RedirectToAction(nameof(Promote));
            }
            catch (Exception ex)
            {
                TempData["Err"] = $"Chyba při povýšení: {ex.Message}";
                return RedirectToAction(nameof(Promote));
            }
        }

        // POST /AdminTrainers/Delete
        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromForm] int id)
        {
            try
            {
                using var con = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand("DEMOTE_TRAINER", (OracleConnection)con)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };
                cmd.Parameters.Add("p_idtrener", OracleDbType.Int32, id, ParameterDirection.Input);
                await cmd.ExecuteNonQueryAsync();

                TempData["Ok"] = "Trenér byl zrušen (ponechán jako člen).";
            }
            catch (OracleException ox) when (ox.Number == 20045)
            {
                TempData["Err"] = "Nelze zrušit trenéra: má přiřazené lekce.";
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Chyba při rušení trenéra: " + ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
