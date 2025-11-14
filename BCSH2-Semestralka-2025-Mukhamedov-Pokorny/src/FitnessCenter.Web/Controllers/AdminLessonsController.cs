using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Application.Services;
using FitnessCenter.Domain.Entities; // Lesson
using FitnessCenter.Infrastructure.Persistence;
using Oracle.ManagedDataAccess.Client;

namespace FitnessCenter.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminLessonsController : Controller
{
    private readonly LessonsService lessons;

    public AdminLessonsController(LessonsService lessons) => this.lessons = lessons;

    // /AdminLessons
    public async Task<IActionResult> Index()
    {
        // 1) načti lekce
        var data = await lessons.GetAllAsync();

        // 2) mapa trenérů { lekceId -> "Jméno Příjmení" }
        var trainers = new Dictionary<int, string>();
        try
        {
            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(@"
                SELECT l.IDLEKCE, NVL(t.JMENO || ' ' || t.PRIJMENI, '-') AS TRENER
                FROM   LEKCE l
                LEFT   JOIN TRENERI t ON t.IDTRENER = l.TRENER_IDTRENER", con);
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var id = rd.GetInt32(0);
                var tn = rd.IsDBNull(1) ? "-" : rd.GetString(1);
                trainers[id] = tn;
            }
        }
        catch { /* necháme bez pádu, ve view se zobrazí '-' */ }

        ViewBag.Trainers = trainers;

        return View(data); // view si rozparsuje „Název @Místo“ stejně jako u Reservations
    }

    // /AdminLessons/Details/123
    public async Task<IActionResult> Details(int id)
    {
        // e-maily účastníků
        var emails = await lessons.GetAttendeeEmailsAsync(id);

        // načti lekci z existujícího GetAllAsync()
        var l = (await lessons.GetAllAsync()).FirstOrDefault(x => x.Id == id);
        if (l is null) return NotFound();

        // stejné parsování názvu/místa jako ve výpisu
        var raw = l.Nazev ?? "";
        string name = raw, place = "";

        var atIdx = raw.LastIndexOf('@');
        if (atIdx >= 0) { name = raw[..atIdx].Trim(); place = raw[(atIdx + 1)..].Trim(); }

        if (string.IsNullOrEmpty(place))
        {
            var open = raw.LastIndexOf('(');
            var close = raw.LastIndexOf(')');
            if (open >= 0 && close > open)
            {
                place = raw.Substring(open + 1, close - open - 1).Trim();
                name = raw.Remove(open).Trim();
            }
        }

        var niceTitle = string.IsNullOrWhiteSpace(place) ? name : $"{name} – {place}";
        ViewBag.LessonId = id;
        ViewBag.LessonTitle = $"{niceTitle} ({l.Zacatek:dd.MM.yyyy HH:mm})";

        return View(emails);
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            var (rel, rez, lek) = await lessons.CancelLessonByAdminAsync(id);

            if (lek == 0)
                TempData["Err"] = "Lekce nebyla nalezena nebo již byla zrušena.";
            else
                TempData["Ok"] = "Zrušeno.";
        }
        catch (Exception ex)
        {
            TempData["Err"] = "Zrušení se nepodařilo: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
