using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Infrastructure.Repositories;
using FitnessCenter.Infrastructure.Persistence;      // DatabaseManager
using Oracle.ManagedDataAccess.Client;              // OracleCommand
using FitnessCenter.Domain.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Member")]
    [Route("[controller]")] // => /Reservations/...
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

                // ⬇⬇⬇ UPRAV PODLE SVÉHO SCHÉMATU ⬇⬇⬇
                // Pokud máš tabulku "rezervace_lekci" a FK se jmenuje jinak, přepiš zde:
                //   - název tabulky s rezervacemi (rezervace / rezervace_lekci / reservations ...)
                //   - název ID rezervace (idrezervace / idrezervace_lekce / id ...)
                //   - název FK na lekci (lekce_idlekce / idlekce / lekce_id ...)
                const string RezTable = "rezervace";        // <-- ZKONTROLUJ
                const string RezIdCol = "idrezervace";      // <-- ZKONTROLUJ
                const string RezLessonFK = "lekce_idlekce";    // <-- ZKONTROLUJ
                                                               // ⬆⬆⬆ UPRAV PODLE SVÉHO SCHÉMATU ⬆⬆⬆

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
                // Nezabij akci – jen nemáme jména trenérů (v UI se ukáže "-")
                // Tip: zkontroluj ve schématu skutečné názvy tabulek/sloupců a přepiš konstanty výše.
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
            try
            {
                var idRez = await _repo.ReserveLessonAsync(idClen, id);
                TempData["ResMsg"] = $"Rezervace vytvořena (#{idRez}).";
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
