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
    public async Task<IActionResult> Index(
        string? when,
        string? search,
        string? sort,
        string? location,
        string? attendeesRange)
    {
        // 1) načti lekce
        var list = (await lessons.GetAllAsync()).ToList();

        // 2) mapy: trenéři + počty účastníků
        var trainers = new Dictionary<int, string>();
        var participants = new Dictionary<int, int>();

        try
        {
            using var con = (OracleConnection)await DatabaseManager.GetOpenConnectionAsync();

            // trenéři
            using (var cmd = new OracleCommand(@"
                SELECT l.IDLEKCE, NVL(t.JMENO || ' ' || t.PRIJMENI, '-') AS TRENER
                FROM   LEKCE l
                LEFT   JOIN TRENERI t ON t.IDTRENER = l.TRENER_IDTRENER", con))
            using (var rd = await cmd.ExecuteReaderAsync())
            {
                while (await rd.ReadAsync())
                {
                    var id = rd.GetInt32(0);
                    var tn = rd.IsDBNull(1) ? "-" : rd.GetString(1);
                    trainers[id] = tn;
                }
            }

            // počty účastníků (RELEKCI)
            using (var cmd2 = new OracleCommand(@"
                SELECT rl.LEKCE_IDLEKCE, COUNT(*) AS pocet
                FROM   RELEKCI rl
                GROUP  BY rl.LEKCE_IDLEKCE", con))
            using (var rd2 = await cmd2.ExecuteReaderAsync())
            {
                while (await rd2.ReadAsync())
                {
                    var id = rd2.GetInt32(0);
                    var cnt = rd2.GetInt32(1);
                    participants[id] = cnt;
                }
            }
        }
        catch
        {
            // když spadne dotaz, necháme trainers/participants prázdné
        }

        // 3) lokality pro filtr (z názvu lekce)
        var allLocations = list
            .Select(l => ExtractPlace(l.Nazev))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        // 4) viewbagy pro view
        ViewBag.Trainers = trainers;
        ViewBag.Participants = participants;
        ViewBag.Locations = allLocations;
        ViewBag.When = when;
        ViewBag.Search = search;
        ViewBag.Sort = sort;
        ViewBag.Location = location;
        ViewBag.AttendeesRange = attendeesRange;

        // 5) filtrování podle názvu lekce
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            list = list
                .Where(l => (l.Nazev ?? string.Empty).ToLower().Contains(s))
                .ToList();
        }

        // 6) filtrování podle času (minulé / budoucí)
        var now = DateTime.Now;

        if (!string.IsNullOrWhiteSpace(when))
        {
            switch (when.ToLower())
            {
                case "future":
                    list = list
                        .Where(l => l.Zacatek >= now)
                        .ToList();
                    break;

                case "past":
                    list = list
                        .Where(l => l.Zacatek < now)
                        .ToList();
                    break;
            }
        }

        // 7) filtrování podle lokality (místo / sál / pobočka)
        if (!string.IsNullOrWhiteSpace(location))
        {
            list = list
                .Where(l => string.Equals(
                    ExtractPlace(l.Nazev),
                    location,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // 8) filtrování podle počtu účastníků
        if (!string.IsNullOrWhiteSpace(attendeesRange))
        {
            list = list
                .Where(l =>
                {
                    participants.TryGetValue(l.Id, out var cnt);
                    return attendeesRange switch
                    {
                        "0" => cnt == 0,
                        "1-5" => cnt >= 1 && cnt <= 5,
                        "6-10" => cnt >= 6 && cnt <= 10,
                        "11plus" => cnt >= 11,
                        _ => true
                    };
                })
                .ToList();
        }

        // 9) řazení – výchozí: nejstarší (nejbližší) nahoře
        list = sort switch
        {
            "newest" => list.OrderByDescending(l => l.Zacatek).ToList(),
            "oldest" => list.OrderBy(l => l.Zacatek).ToList(),
            _ => list.OrderBy(l => l.Zacatek).ToList()
        };

        return View(list);
    }

    // pomocná funkce na parsování lokality z názvu lekce
    private static string ExtractPlace(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var text = raw;

        // varianta "Název @Místo"
        var atIdx = text.LastIndexOf('@');
        if (atIdx >= 0)
        {
            var place = text[(atIdx + 1)..].Trim();
            if (!string.IsNullOrEmpty(place))
                return place;

            text = text[..atIdx].Trim();
        }

        // varianta "Název (Místo)"
        var open = text.LastIndexOf('(');
        var close = text.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            return text.Substring(open + 1, close - open - 1).Trim();
        }

        return "";
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
