using FitnessCenter.Application.Services;
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Models.Lessons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Trainer")]
    public sealed class TrainerLessonsController : Controller
    {
        private readonly LessonsService lessons;

        public TrainerLessonsController(LessonsService lessons)
        {
            this.lessons = lessons;
        }

        // /TrainerLessons/Calendar
        public async Task<IActionResult> Calendar(int? year, int? month)
        {
            var today = DateTime.Today;

            int y = (year is > 0) ? year.Value : today.Year;
            int m = (month is >= 1 and <= 12) ? month.Value : today.Month;

            var firstDay = new DateTime(y, m, 1);
            var nextMonth = firstDay.AddMonths(1);

            // 1) LEKCE v daném měsíci
            var allLessons = (await lessons.GetAllAsync()).ToList();

            // Pokud chceš, aby trenér viděl jen SVOJE lekce,
            // tady by se dalo filtrovat podle trenéra.
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

            // 2) ADMIN AKCE z KALENDAR_AKCE – jen na čtení
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

            ViewBag.Active = "HomeTrainer"; // aby v menu svítil „Trenér“
            return View(vm);
        }
    }
}
