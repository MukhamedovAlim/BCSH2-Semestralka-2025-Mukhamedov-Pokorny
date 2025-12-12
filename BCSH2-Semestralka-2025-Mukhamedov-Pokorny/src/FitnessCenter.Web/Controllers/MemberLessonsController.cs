using FitnessCenter.Application.Services;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.Models.Lessons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Member")]
    public sealed class MemberLessonsController : Controller
    {
        private readonly LessonsService lessons;

        public MemberLessonsController(LessonsService lessons)
            => this.lessons = lessons;

        // ===== KALENDÁŘ PRO ČLENA (jen čtení) =====
        public async Task<IActionResult> Calendar(int? year, int? month)
        {
            var today = DateTime.Today;

            int y = (year is > 0) ? year.Value : today.Year;
            int m = (month is >= 1 and <= 12) ? month.Value : today.Month;

            var firstDay = new DateTime(y, m, 1);
            var nextMonth = firstDay.AddMonths(1);

            // 1) LEKCE v daném měsíci
            var allLessons = (await lessons.GetAllAsync()).ToList();

            var lessonItems = allLessons
                .Where(l => l.Zacatek >= firstDay && l.Zacatek < nextMonth)
                .OrderBy(l => l.Zacatek)
                .Select(l => new LessonCalendarItem
                {
                    Id = l.Id,
                    Nazev = l.Nazev ?? "",
                    Zacatek = l.Zacatek,
                    Kapacita = l.Kapacita
                })
                .ToList();

            // 2) ADMIN AKCE (KALENDAR_AKCE) – jen čtení
            var events = new List<CalendarAdminEventItem>();

            using (var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync())
            using (var cmd = new OracleCommand(@"
                    SELECT IDAKCE, DATUM, TYP, POPIS, FITNESSCENTRUM_IDFITNESS
                      FROM KALENDAR_AKCE
                     WHERE DATUM >= :od
                       AND DATUM < :do
                     ORDER BY DATUM, IDAKCE", con)
            { BindByName = true })
            {
                cmd.Parameters.Add("od", OracleDbType.Date).Value = firstDay;
                cmd.Parameters.Add("do", OracleDbType.Date).Value = nextMonth;

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    events.Add(new CalendarAdminEventItem
                    {
                        Id = rd.GetInt32(0),
                        Date = rd.GetDateTime(1),
                        Type = rd.GetString(2),
                        Text = rd.GetString(3),
                        FitnessId = rd.IsDBNull(4) ? (int?)null : rd.GetInt32(4)
                    });
                }
            }

            var vm = new LessonsCalendarViewModel
            {
                Year = y,
                Month = m,
                LessonsByDay = lessonItems
                    .GroupBy(x => x.Zacatek.Day)
                    .ToDictionary(g => g.Key, g => g.ToList()),
                EventsByDay = events
                    .GroupBy(e => e.Date.Day)
                    .ToDictionary(g => g.Key, g => g.ToList())
            };

            ViewBag.Active = "MemberCalendar";
            return View(vm);
        }

        public async Task<IActionResult> DetailDialog(int id)
        {
            var lesson = await lessons.GetAsync(id);
            if (lesson == null)
                return Content("Lekce nenalezena.");

            string trainerName = "";

            using (var con = await DatabaseManager.GetOpenConnectionAsync())
            using (var cmd = new OracleCommand(@"
        SELECT t.jmeno || ' ' || t.prijmeni
        FROM treneri t
        JOIN lekce l ON l.trener_idtrener = t.idtrener
        WHERE l.idlekce = :id", con))
            {
                cmd.Parameters.Add("id", id);

                var r = await cmd.ExecuteScalarAsync();
                if (r != null)
                    trainerName = r.ToString();
            }

            // počet rezervací
            int reserved = await lessons.GetAttendeesAsync(id)
                                        .ContinueWith(t => t.Result.Count);

            var vm = new LessonDetailDialogViewModel
            {
                Id = lesson.Id,
                Nazev = lesson.Nazev,
                Zacatek = lesson.Zacatek,
                Kapacita = lesson.Kapacita,
                Reserved = reserved,
                TrainerName = trainerName
            };

            return PartialView("_LessonDetailDialog", vm);
        }

    }
}
