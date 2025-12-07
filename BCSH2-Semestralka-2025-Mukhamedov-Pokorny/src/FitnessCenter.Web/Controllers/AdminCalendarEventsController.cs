using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models.Lessons;
using Oracle.ManagedDataAccess.Client;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public sealed class AdminCalendarEventsController : Controller
    {
        [HttpGet]
        public IActionResult Create(string? date)
        {
            DateTime dt;

            if (!string.IsNullOrEmpty(date) &&
                DateTime.TryParse(date, out var parsed))
            {
                dt = parsed.Date;
            }
            else
            {
                dt = DateTime.Today;
            }

            var model = new CalendarEventCreateViewModel
            {
                Date = dt,
                Type = ""
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CalendarEventCreateViewModel model)
        {

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

                const string sql = @"
            INSERT INTO KALENDAR_AKCE
                (IDAKCE, DATUM, TYP, POPIS, FITNESSCENTRUM_IDFITNESS, CREATED_BY)
            VALUES
                (S_KALENDAR_AKCE.NEXTVAL, :d, :t, :p, :f, :who)";

                using var cmd = new OracleCommand(sql, con) { BindByName = true };
                cmd.Parameters.Add("d", OracleDbType.Date).Value = model.Date.Date;
                cmd.Parameters.Add("t", OracleDbType.Varchar2).Value = model.Type ?? "";
                cmd.Parameters.Add("p", OracleDbType.Varchar2).Value = model.Text ?? "";
                cmd.Parameters.Add("f", OracleDbType.Int32).Value =
                    (object?)model.FitnessId ?? DBNull.Value;
                cmd.Parameters.Add("who", OracleDbType.Varchar2).Value =
                    User?.Identity?.Name ?? "admin";

                await cmd.ExecuteNonQueryAsync();

                TempData["Ok"] = "Akce byla uložena do kalendáře.";
                return RedirectToAction("Calendar", "AdminLessons",
                    new { year = model.Date.Year, month = model.Date.Month });
            }
            catch (Exception ex)
            {
                Console.WriteLine("DEBUG >>> EX: " + ex);
                ModelState.AddModelError(string.Empty, "Uložení se nepodařilo: " + ex.Message);
                return View(model);
            }
        }

        // ====== EDIT GET ======
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

            const string sql = @"
                SELECT IDAKCE, DATUM, TYP, POPIS, FITNESSCENTRUM_IDFITNESS
                  FROM KALENDAR_AKCE
                 WHERE IDAKCE = :id";

            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;

            using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync())
                return NotFound();

            var model = new CalendarEventCreateViewModel
            {
                Id = rd.GetInt32(0),
                Date = rd.GetDateTime(1),
                Type = rd.GetString(2),
                Text = rd.GetString(3),
                FitnessId = rd.IsDBNull(4) ? (int?)null : rd.GetInt32(4)
            };

            // použijeme stejný view jako pro Create
            return View("Create", model);
        }

        // ====== EDIT POST ======
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CalendarEventCreateViewModel model)
        {
            if (!ModelState.IsValid || model.Id is null)
                return View("Create", model);

            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

            const string sql = @"
                UPDATE KALENDAR_AKCE
                   SET DATUM = :d,
                       TYP   = :t,
                       POPIS = :p,
                       FITNESSCENTRUM_IDFITNESS = :f
                 WHERE IDAKCE = :id";

            using var cmd = new OracleCommand(sql, con) { BindByName = true };
            cmd.Parameters.Add("d", OracleDbType.Date).Value = model.Date.Date;
            cmd.Parameters.Add("t", OracleDbType.Varchar2).Value = model.Type;
            cmd.Parameters.Add("p", OracleDbType.Varchar2).Value = model.Text;
            cmd.Parameters.Add("f", OracleDbType.Int32).Value =
                (object?)model.FitnessId ?? DBNull.Value;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = model.Id.Value;

            await cmd.ExecuteNonQueryAsync();

            TempData["Ok"] = "Akce byla upravena.";

            return RedirectToAction("Calendar", "AdminLessons",
                new { year = model.Date.Year, month = model.Date.Month });
        }

        [HttpGet]
        public async Task<IActionResult> DeleteQuick(int id, int year, int month)
        {
            try
            {
                using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

                const string sql = @"DELETE FROM KALENDAR_AKCE WHERE IDAKCE = :id";

                using var cmd = new OracleCommand(sql, con) { BindByName = true };
                cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;

                var rows = await cmd.ExecuteNonQueryAsync();

                TempData["Ok"] = rows == 0
                    ? "Akce už neexistuje."
                    : "Akce byla smazána.";
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Smazání se nepodařilo: " + ex.Message;
            }

            // návrat na kalendář do stejného měsíce
            return RedirectToAction("Calendar", "AdminLessons",
                new { year, month });
        }
    }
}