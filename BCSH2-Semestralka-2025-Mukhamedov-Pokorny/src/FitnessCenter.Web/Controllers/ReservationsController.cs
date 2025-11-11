using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Persistence;      // DatabaseManager
using FitnessCenter.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;              // OracleCommand
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Member")]
    [Route("[controller]")]
    public class ReservationsController : Controller
    {
        private readonly OracleLessonsRepository _repo;
        private readonly PaymentsReadRepo _members;

        public ReservationsController(OracleLessonsRepository repo, PaymentsReadRepo members)
        {
            _repo = repo;
            _members = members;
        }

        private async Task<int> ResolveMemberId()
        {
            if (int.TryParse(User.FindFirst("ClenId")?.Value, out var id) && id > 0)
                return id;

            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrWhiteSpace(email)) return 0;

            return (await _members.GetMemberIdByEmailAsync(email)) ?? 0;
        }

        // helper: načte volná místa pro dané lekce z v_lekce_volne
        private static async Task<Dictionary<int, int>> LoadVolnoByLessonIdsAsync(IEnumerable<int> lessonIds)
        {
            var ids = lessonIds?.Distinct().Where(i => i > 0).ToList() ?? new();
            var map = new Dictionary<int, int>();
            if (ids.Count == 0) return map;

            using var con = await DatabaseManager.GetOpenConnectionAsync();
            var paramNames = ids.Select((_, i) => $":p{i}").ToArray();

            var sql = $@"
        SELECT idlekce, volno
        FROM v_lekce_volne
        WHERE idlekce IN ({string.Join(",", paramNames)})
    ";

            using var cmd = new OracleCommand(sql, (OracleConnection)con) { BindByName = true };
            for (int i = 0; i < ids.Count; i++) cmd.Parameters.Add($"p{i}", OracleDbType.Int32).Value = ids[i];

            using var rd = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (await rd.ReadAsync())
                map[rd.GetInt32(0)] = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);

            return map;
        }

        // spočítá volná místa pro zadané lekce
        private static async Task<Dictionary<int, int>> LoadFreeSlotsMapAsync(IEnumerable<int> lessonIds)
        {
            var ids = lessonIds?.Distinct().Where(i => i > 0).ToList() ?? new List<int>();
            var map = new Dictionary<int, int>();
            if (ids.Count == 0) return map;

            using var con = await DatabaseManager.GetOpenConnectionAsync();
            var p = ids.Select((_, i) => $":p{i}").ToArray();

            var sql = $@"
        SELECT l.idlekce,
               GREATEST(l.obsazenost - COUNT(r.rezervacelekci_idrezervace), 0) AS volno
          FROM lekce l
          LEFT JOIN relekci r ON r.lekce_idlekce = l.idlekce
         WHERE l.idlekce IN ({string.Join(",", p)})
         GROUP BY l.idlekce, l.obsazenost";

            using var cmd = new OracleCommand(sql, (OracleConnection)con) { BindByName = true };
            for (int i = 0; i < ids.Count; i++) cmd.Parameters.Add($"p{i}", ids[i]);

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                map[rd.GetInt32(0)] = rd.GetInt32(1);

            return map;
        }


        // ===================== pomocné loadery trenérů =====================

        // lekceId -> "Jméno Příjmení"
        private static async Task<Dictionary<int, string>> LoadTrainersByLessonIdsAsync(IEnumerable<int> lessonIds)
        {
            var ids = lessonIds?.Distinct().Where(i => i > 0).ToList() ?? new List<int>();
            var map = new Dictionary<int, string>();
            if (ids.Count == 0) return map;

            using var con = await DatabaseManager.GetOpenConnectionAsync();

            // Oracle: bezpečný IN s parametry
            var p = ids.Select((_, i) => $":p{i}").ToArray();
            var sql = $@"
        SELECT l.idlekce,
               TRIM(NVL(t.jmeno, '') || ' ' || NVL(t.prijmeni, '')) AS trener
          FROM lekce l
          LEFT JOIN treneri t ON t.idtrener = l.trener_idtrener
         WHERE l.idlekce IN ({string.Join(",", p)})
    ";

            using var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand(sql, (Oracle.ManagedDataAccess.Client.OracleConnection)con)
            { BindByName = true };

            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.Add($"p{i}", ids[i]);

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var lessonId = rd.GetInt32(0);
                var trainer = rd.IsDBNull(1) ? "-" : rd.GetString(1);
                map[lessonId] = string.IsNullOrWhiteSpace(trainer) ? "-" : trainer;
            }

            return map;
        }

        // rezervaceId -> "Jméno Příjmení" (rezervace → lekce → trenér)
        private static async Task<Dictionary<int, string>> LoadTrainersByReservationIdsAsync(IEnumerable<int> rezIds)
        {
            var ids = rezIds?.Distinct().Where(i => i > 0).ToList() ?? new List<int>();
            var map = new Dictionary<int, string>();
            if (ids.Count == 0) return map;

            try
            {
                using var con = await DatabaseManager.GetOpenConnectionAsync();

                const string RezTable = "rezervace";
                const string RezIdCol = "idrezervace";
                const string RezLessonFK = "lekce_idlekce";

                var p = ids.Select((_, i) => $":p{i}").ToArray();
                var sql = $@"
            SELECT r.{RezIdCol}, (t.jmeno || ' ' || t.prijmeni) AS trener
              FROM {RezTable} r
              JOIN lekce l   ON l.idlekce = r.{RezLessonFK}
              JOIN treneri t ON t.idtrener = l.trener_idtrener
             WHERE r.{RezIdCol} IN ({string.Join(",", p)})
        ";

                using var cmd = new OracleCommand(sql, (OracleConnection)con) { BindByName = true };
                for (int i = 0; i < ids.Count; i++) cmd.Parameters.Add($"p{i}", ids[i]);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    map[rd.GetInt32(0)] = rd.IsDBNull(1) ? "-" : rd.GetString(1);
            }
            catch (OracleException ex) when (ex.Number == 942) // ORA-00942: tabulka/pohled neexistuje
            {
            }

            return map;
        }


        // ============================ AKCE ============================

        // GET /Reservations
        // GET /Reservations
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var list = await _repo.GetUpcomingViaProcAsync();

            ViewBag.Trainers = await LoadTrainersByLessonIdsAsync(list.Select(l => l.Id));
            ViewBag.VolnoMap = await LoadFreeSlotsMapAsync(list.Select(l => l.Id));   // ⬅️ DŮLEŽITÉ

            // membership do UI (jak už máš)
            var idClen = await ResolveMemberId();
            var ms = await _members.GetMembershipAsync(idClen);
            ViewBag.MembershipActive = ms.Active;
            ViewBag.MembershipFrom = ms.From;
            ViewBag.MembershipTo = ms.To;

            return View(list);
        }


        // GET /Reservations/Mine
        [HttpGet("Mine")]
        public async Task<IActionResult> Mine()
        {
            var idClen = await ResolveMemberId();
            var rows = await _repo.GetMyReservationsViaProcAsync(idClen);

            // 1) sebereme ID lekcí z mých rezervací
            var lessonIds = rows.Select(r => r.IdLekce).Where(i => i > 0).Distinct();

            // 2) dotáhneme jména trenérů pro tyto lekce
            var trainerMap = await LoadTrainersByLessonIdsAsync(lessonIds);

            // 3) pošleme do view
            ViewBag.Trainers = trainerMap;

            return View(rows);
        }


        // POST /Reservations/Book/{id}
        [HttpPost("Book/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(int id)
        {
            var idClen = await ResolveMemberId();

            // 1) zjistíme lekci kvůli datu
            var lesson = await _repo.GetByIdAsync(id);
            if (lesson == null)
            {
                TempData["ResMsg"] = "Lekce neexistuje.";
                return RedirectToAction(nameof(Index));
            }

            // 2) membership z DB (stejně to ještě ohlídá trigger/procedura, ale UX bude v pohodě)
            var ms = await _members.GetMembershipAsync(idClen);

            if (!(ms.Active && ms.From.HasValue && ms.To.HasValue))
            {
                TempData["ResMsg"] = "Rezervaci nelze vytvořit: nemáš aktivní členství.";
                return RedirectToAction(nameof(Index));
            }

            var d = lesson.Zacatek.Date;
            if (d < ms.From.Value.Date || d > ms.To.Value.Date)
            {
                TempData["ResMsg"] = "Rezervaci nelze vytvořit: lekce je mimo platnost tvého členství.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var idRez = await _repo.ReserveLessonAsync(idClen, id);
                TempData["ResMsg"] = "Rezervace vytvořena.";
                return RedirectToAction(nameof(Mine));
            }
            catch (Exception ex)
            {
                TempData["ResMsg"] = "Rezervaci se nepodařilo vytvořit: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST /Reservations/Cancel
        [HttpPost("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel([FromForm] int rezId)
        {
            var idClen = await ResolveMemberId();
            await _repo.CancelReservationByIdAsync(idClen, rezId);
            TempData["ResMsg"] = "Rezervace byla zrušena.";
            return RedirectToAction(nameof(Mine));
        }
    }
}
