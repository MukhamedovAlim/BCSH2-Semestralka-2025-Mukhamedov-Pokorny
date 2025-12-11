using FitnessCenter.Application.Interfaces;      // ILessonsService, IMembersService
using FitnessCenter.Infrastructure.Persistence;  // DatabaseManager
using FitnessCenter.Infrastructure.Repositories; // PaymentsReadRepo, AdminStatsRepository
using FitnessCenter.Web.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace FitnessCenter.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly PaymentsReadRepo _payments;
        private readonly ILessonsService _lessons;
        private readonly IMembersService _members;
        private readonly AdminStatsRepository _stats;
        private readonly DashboardRepository _dashboard;

        public HomeController(
            PaymentsReadRepo payments,
            ILessonsService lessons,
            IMembersService members,
            AdminStatsRepository stats,
            DashboardRepository dashboard)
        {
            _payments = payments;
            _lessons = lessons;
            _members = members;
            _stats = stats;
            _dashboard = dashboard;
        }

        // ===== Member dashboard =====
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Active = "Home";

            int clenId = User.GetRequiredCurrentMemberId();

            // načíst člena kvůli kontaktu
            var member = await _members.GetByIdAsync(clenId);

            if (member == null)
            {
                ViewBag.MemberPhone = "—";
                ViewBag.MemberEmail = "—";
            }
            else
            {
                ViewBag.MemberPhone = string.IsNullOrWhiteSpace(member.Phone)
                    ? "—"
                    : member.Phone;

                ViewBag.MemberEmail = string.IsNullOrWhiteSpace(member.Email)
                    ? "—"
                    : member.Email;
            }

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
        public async Task<IActionResult> Trainer(CancellationToken ct)
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

            // všechny lekce trenéra
            var all = await _lessons.GetForTrainerAsync(trainerId.Value);

            // dnešní lekce jako list (budeme je používat 2×)
            var todaysLessons = all
                .Where(l => l.Zacatek >= today && l.Zacatek < tomorrow)
                .OrderBy(l => l.Zacatek)
                .ToList();

            // spočítáme obsazenost (počet rezervovaných) pro každou dnešní lekci
            var reservedMap = new Dictionary<int, int>();
            foreach (var l in todaysLessons)
            {
                var attendees = await _lessons.GetAttendeesAsync(l.Id, ct);
                reservedMap[l.Id] = attendees.Count;
            }

            // naplníme objekt pro view
            var todays = todaysLessons
                .Select(l => new
                {
                    Id = l.Id,
                    Time = l.Zacatek.ToString("HH:mm"),
                    Name = l.Nazev,
                    Room = string.IsNullOrWhiteSpace(l.Mistnost) ? "—" : l.Mistnost,
                    Capacity = l.Kapacita,
                    Reserved = reservedMap.TryGetValue(l.Id, out var cnt) ? cnt : 0
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

            // ===== 1) Parametry pro FUNKČNÍ boxy (aktivní členové, příjem, hodnocení) =====
            int fitkoId = fitko.GetValueOrDefault(1);
            var from = od ?? DateTime.Today.AddDays(-30);   // pro funkce
            var to = @do ?? DateTime.Today;

            // seznam členů pro vybrané fitko (pro dropdown)
            ViewBag.MembersInFitness = await LoadMembersInFitnessAsync(fitkoId);
            ViewBag.MemberId = memberId; // zapamatujeme si, co bylo zvoleno

            int members = 0, trainers = 0, lessonsCount = 0, pendingPayments = 0, logCount = 0, equipmentCount = 0;

            // === kolekce pro nový graf – stav vybavení podle fitka ===
            var equipLabels = new List<string>();
            var equipOkCounts = new List<int>();
            var equipRepairCounts = new List<int>();
            var equipOutCounts = new List<int>();

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

                // === nový SELECT – stav vybavení podle fitness centra ===
                const string equipSql = @"
SELECT fc.nazev,
       SUM(CASE WHEN v.stav = 'OK'          THEN 1 ELSE 0 END) AS ok_count,
       SUM(CASE WHEN v.stav = 'Oprava'      THEN 1 ELSE 0 END) AS repair_count,
       SUM(CASE WHEN v.stav = 'Mimo provoz' THEN 1 ELSE 0 END) AS out_count
FROM   vybaveni v
JOIN   fitnesscentra fc ON fc.idfitness = v.fitnesscentrum_idfitness
GROUP  BY fc.nazev
ORDER  BY fc.nazev";

                using (var cmd = new OracleCommand(equipSql, oc) { BindByName = true })
                using (var rd = await cmd.ExecuteReaderAsync())
                {
                    while (await rd.ReadAsync())
                    {
                        equipLabels.Add(rd.GetString(0));
                        equipOkCounts.Add(rd.IsDBNull(1) ? 0 : rd.GetInt32(1));
                        equipRepairCounts.Add(rd.IsDBNull(2) ? 0 : rd.GetInt32(2));
                        equipOutCounts.Add(rd.IsDBNull(3) ? 0 : rd.GetInt32(3));
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Nepodařilo se načíst statistiky: " + ex.Message;
            }

            ViewBag.MembersCount = members;
            ViewBag.TrainersCount = trainers;
            ViewBag.LessonsToday = lessonsCount; // (pokud chceš fakt "dnešní", musí se dotaz upravit)
            ViewBag.PendingPayments = pendingPayments;
            ViewBag.AdminMessagesCnt = 0;
            ViewBag.LogCount = logCount;
            ViewBag.EquipmentCount = equipmentCount;

            // >>> data pro nový graf vybavení <<<
            ViewBag.EquipCenterLabels = equipLabels;
            ViewBag.EquipOkCounts = equipOkCounts;
            ViewBag.EquipRepairCounts = equipRepairCounts;
            ViewBag.EquipOutCounts = equipOutCounts;

            // ===== 3 FUNKCE (aktivní členové, příjem, hodnocení) =====
            try
            {
                // 1) aktivní členové ve fitku
                ViewBag.ActiveMembersFunc = await _stats.GetActiveMembersAsync(fitkoId);

                // 2) příjem za zvolené období (from/to z filtru)
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

            // ===== DATA PRO GRAF TRŽEB – VLASTNÍ OBDOBÍ (nezávislé na from/to) =====
            try
            {
                var revTo = DateTime.Today;

                // 1) Zjistíme nejstarší datum platby v DB
                DateTime revFrom;

                using (var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync())
                using (var cmd = new OracleCommand("SELECT MIN(TRUNC(datumplatby)) FROM platby", con))
                {
                    var obj = await cmd.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value)
                    {
                        revFrom = ((DateTime)obj).Date;
                    }
                    else
                    {
                        // fallback – kdyby v DB nebyly žádné platby
                        revFrom = revTo.AddDays(-30);
                    }
                }

                // 2) Omezíme začátek MINIMÁLNĚ na 20.10. tohoto roku,
                //    aby graf nebyl zbytečně roztažený dozadu
                var hardMin = new DateTime(revTo.Year, 10, 20);
                if (revFrom < hardMin)
                    revFrom = hardMin;

                // 3) Natáhneme data pro graf (od revFrom do dneška)
                var (days, revenue) = await _dashboard.GetDailyRevenueAsync(revFrom, revTo);
                ViewBag.RevenueLabels = days;
                ViewBag.RevenueValues = revenue;
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Chyba při načítání dat pro graf tržeb: " + ex.Message;
            }

            // >>> DATA PRO PIE CHART – PODÍL TYPŮ ČLENSTVÍ <<<
            try
            {
                var (types, counts) = await _dashboard.GetMembershipDistributionAsync();
                ViewBag.MemberTypes = types;
                ViewBag.MemberCounts = counts;
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Chyba při načítání podílu typů členství: " + ex.Message;
            }

            // >>> DATA PRO BAR CHART – TOP TRENÉŘI PODLE POČTU REZERVACÍ <<<
            try
            {
                var (topTrainers, topCounts) = await _dashboard.GetTopTrainersAsync();
                ViewBag.TopTrainerNames = topTrainers;
                ViewBag.TopTrainerCounts = topCounts;
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Chyba při načítání nejvytíženějších trenérů: " + ex.Message;
            }

            // from/to zůstává pro funkční boxy (příjem za období)
            ViewBag.FitkoId = fitkoId;
            ViewBag.PeriodFrom = from;
            ViewBag.PeriodTo = to;

            return View();
        }
    }
}