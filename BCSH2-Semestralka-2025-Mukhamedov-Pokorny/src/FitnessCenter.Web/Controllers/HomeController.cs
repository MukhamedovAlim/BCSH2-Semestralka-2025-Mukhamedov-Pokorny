using FitnessCenter.Application.Interfaces;      // ILessonsService, IMembersService
using FitnessCenter.Infrastructure.Persistence;  // DatabaseManager
using FitnessCenter.Infrastructure.Repositories; // PaymentsReadRepo, AdminStatsRepository
using FitnessCenter.Web.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FitnessCenter.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly PaymentsReadRepo _payments;
        private readonly ILessonsService _lessons;
        private readonly IMembersService _members;
        private readonly AdminStatsRepository _stats;

        public HomeController(
            PaymentsReadRepo payments,
            ILessonsService lessons,
            IMembersService members,
            AdminStatsRepository stats)
        {
            _payments = payments;
            _lessons = lessons;
            _members = members;
            _stats = stats;
        }

        // ===== Member dashboard =====
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Active = "Home";

            int clenId = User.GetRequiredCurrentMemberId();

            // načíst člena kvůli kontaktu
            var member = await _members.GetByIdAsync(clenId);

            ViewBag.MemberPhone = string.IsNullOrWhiteSpace(member.Phone)
                ? "—"
                : member.Phone;

            ViewBag.MemberEmail = string.IsNullOrWhiteSpace(member.Email)
                ? "—"
                : member.Email;

            // data pro permanentku
            var ms = await _payments.GetMembershipAsync(clenId);
            ViewBag.permActive = ms.Active;
            ViewBag.permType = ms.TypeName ?? "-";
            ViewBag.permPrice = "-";

            if (ms.Active && ms.From.HasValue && ms.To.HasValue)
            {
                var total = (ms.To.Value.Date - ms.From.Value.Date).TotalDays;
                var used = (DateTime.Today - ms.From.Value.Date).TotalDays;
                var usedPct = total <= 0 ? 100 : Math.Clamp((int)Math.Round(100 * used / total), 0, 100);

                ViewBag.permFrom = ms.From;
                ViewBag.permTo = ms.To;
                ViewBag.daysLeft = ms.DaysLeft;
                ViewBag.usedPct = usedPct;
            }
            else
            {
                ViewBag.permFrom = (DateTime?)null;
                ViewBag.permTo = (DateTime?)null;
                ViewBag.daysLeft = 0;
                ViewBag.usedPct = 0;
            }

            return View();
        }

        // ===== Trainer dashboard – dnešní lekce =====
        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> Trainer()
        {
            ViewBag.Active = "HomeTrainer";

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var email = User.FindFirstValue(ClaimTypes.Email);
            var trainerId = string.IsNullOrWhiteSpace(email)
                ? (int?)null
                : await _members.GetTrainerIdByEmailAsync(email);

            if (trainerId is null)
            {
                TempData["Err"] = "K účtu není přiřazen žádný trenér.";
                ViewBag.Today = today;
                ViewBag.LessonsToday = Array.Empty<object>();
                return View();
            }

            var all = await _lessons.GetForTrainerAsync(trainerId.Value);

            var todays = all
                .Where(l => l.Zacatek >= today && l.Zacatek < tomorrow)
                .OrderBy(l => l.Zacatek)
                .Select(l => new
                {
                    Id = l.Id,
                    Time = l.Zacatek.ToString("HH:mm"),
                    Name = l.Nazev,
                    Room = string.IsNullOrWhiteSpace(l.Mistnost) ? "—" : l.Mistnost,
                    Slots = l.Kapacita.ToString()
                })
                .ToList();

            ViewBag.Today = today;
            ViewBag.LessonsToday = todays;

            return View();
        }

        private static async Task<List<SelectListItem>> LoadMembersInFitnessAsync(int fitkoId)
        {
            var items = new List<SelectListItem>();

            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(
                @"SELECT idclen, jmeno, prijmeni
            FROM clenove
           WHERE fitnesscentrum_idfitness = :fid
        ORDER BY prijmeni, jmeno", con)
            { BindByName = true };

            cmd.Parameters.Add("fid", OracleDbType.Int32).Value = fitkoId;

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var id = rd.GetInt32(0);
                var fullName = $"{rd.GetString(2)} {rd.GetString(1)}"; // Přijmení Jméno
                items.Add(new SelectListItem
                {
                    Value = id.ToString(),
                    Text = fullName
                });
            }

            return items;
        }

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

        // ===== Admin dashboard – statistiky + 3 funkce =====
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Admin(int? fitko = null, DateTime? od = null, DateTime? @do = null, int? memberId = null)
        {
            ViewBag.Active = "Admin";
            ViewBag.Today = DateTime.Today;
            ViewBag.HideMainNav = true;
            ViewBag.FitnessCenters = await LoadFitnessCentersAsync();
            ViewBag.Fitka = ViewBag.FitnessCenters;

            // parametry pro funkční boxy
            int fitkoId = fitko.GetValueOrDefault(1);
            var from = od ?? DateTime.Today.AddDays(-30);
            var to = @do ?? DateTime.Today;

            // seznam členů pro vybrané fitko (pro dropdown)
            ViewBag.MembersInFitness = await LoadMembersInFitnessAsync(fitkoId);
            ViewBag.MemberId = memberId; // zapamatujeme si, co bylo zvoleno

            int members = 0, trainers = 0, lessonsCount = 0, pendingPayments = 0, logCount = 0, equipmentCount = 0;

            try
            {
                using var con = await DatabaseManager.GetOpenConnectionAsync();
                var oc = (OracleConnection)con;

                using (var cmd = new OracleCommand("SELECT COUNT(*) FROM CLENOVE", oc))
                    members = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                using (var cmd = new OracleCommand("SELECT COUNT(*) FROM TRENERI", oc))
                    trainers = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                var sqlLessons = @"
SELECT COUNT(*)
FROM   LEKCE l
WHERE  (:p_fitko IS NULL OR EXISTS (
           SELECT 1
             FROM TRENERI t
             JOIN CLENOVE c ON LOWER(c.EMAIL) = LOWER(t.EMAIL)
            WHERE t.IDTRENER = l.TRENER_IDTRENER
              AND c.FITNESSCENTRUM_IDFITNESS = :p_fitko
       ))";

                using (var cmd = new OracleCommand(sqlLessons, oc) { BindByName = true })
                {
                    cmd.Parameters.Add("p_fitko", OracleDbType.Int32).Value =
                        fitko.HasValue ? (object)fitko.Value : DBNull.Value;
                    lessonsCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                using (var cmd = new OracleCommand(
                    "SELECT COUNT(*) FROM PLATBY WHERE STAVPLATBY_IDSTAVPLATBY = 1", oc))
                    pendingPayments = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                using (var cmd = new OracleCommand("SELECT COUNT(*) FROM LOG_OPERACE", oc))
                    logCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                using (var cmd = new OracleCommand("SELECT COUNT(*) FROM VYBAVENI", oc))
                    equipmentCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Nepodařilo se načíst statistiky: " + ex.Message;
            }

            ViewBag.MembersCount = members;
            ViewBag.TrainersCount = trainers;
            ViewBag.LessonsToday = lessonsCount; // teď obsahuje VŠECHNY lekce
            ViewBag.PendingPayments = pendingPayments;
            ViewBag.AdminMessagesCnt = 0;
            ViewBag.LogCount = logCount;
            ViewBag.EquipmentCount = equipmentCount;

            // 3 FUNKCE
            try
            {
                // 1) aktivní členové ve fitku
                ViewBag.ActiveMembersFunc = await _stats.GetActiveMembersAsync(fitkoId);

                // 2) příjem za období
                ViewBag.IncomeFunc = await _stats.GetIncomeAsync(from, to);

                // 3) hodnocení člena
                int? effectiveMemberId = memberId;

                if (effectiveMemberId.HasValue)
                {
                    ViewBag.MemberFuncText = await _stats.GetMemberRatingAsync(effectiveMemberId.Value);
                }
                else
                {
                    ViewBag.MemberFuncText = "Vyberte člena ve filtru výše.";
                }

                ViewBag.MemberId = effectiveMemberId; // aby se dropdown předvyplnil
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Chyba při volání funkcí: " + ex.Message;
            }

            ViewBag.FitkoId = fitkoId;
            ViewBag.PeriodFrom = from;
            ViewBag.PeriodTo = to;

            return View();
        }
    }
}
