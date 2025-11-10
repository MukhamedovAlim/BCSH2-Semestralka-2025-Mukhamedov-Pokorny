using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Application.Services;
using FitnessCenter.Domain.Entities; // Lesson

namespace FitnessCenter.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminLessonsController : Controller
{
    private readonly LessonsService lessons;

    public AdminLessonsController(LessonsService lessons) => this.lessons = lessons;

    // /AdminLessons
    public async Task<IActionResult> Index()
    {
        // prostě zobraz všechny lekce (stejně jako u trenéra, jen bez filtru)
        var data = await lessons.GetAllAsync();   // už máš
        return View(data);
    }

    // /AdminLessons/Details/123
    public async Task<IActionResult> Details(int id)
    {
        // využij existující repo metodu s e-maily účastníků
        var emails = await lessons.GetAttendeeEmailsAsync(id); // už máš v repo
        ViewBag.LessonId = id;
        return View(emails);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            var (rel, rez, lek) = await lessons.CancelLessonByAdminAsync(id);

            if (lek == 0)
                TempData["Err"] = $"Lekce nebyla nalezena nebo již byla zrušena.";
            else
                TempData["Ok"] = $"Zrušeno.";

        }
        catch (Exception ex)
        {
            TempData["Err"] = "Zrušení se nepodařilo: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

}
