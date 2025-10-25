using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FitnessCenter.Web.Models;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Trainers")]
    public class AdminTrainersController : Controller
    {
        // reuse stejného in-memory/servisního zdroje jako máš v TrainerControlleru
        private static readonly List<TrainerViewModel> _data = TrainerController.Data; // viz níže

        [HttpGet("")]
        public IActionResult Index()
        {
            ViewBag.Active = "Admin"; // zvýrazni tab „Admin“ v navbaru
            var list = _data.OrderBy(t => t.LastName).ThenBy(t => t.FirstName).ToList();
            // Reuse existujícího view bez kopírování souborů:
            return View("~/Views/Trainer/Index.cshtml", list);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            ViewBag.Active = "Admin";
            return View("~/Views/Trainer/Create.cshtml", new TrainerViewModel());
        }

        [HttpPost("Create"), ValidateAntiForgeryToken]
        public IActionResult CreatePost(TrainerViewModel vm)
        {
            if (!ModelState.IsValid) return View("~/Views/Trainer/Create.cshtml", vm);
            vm.TrainerId = _data.Any() ? _data.Max(x => x.TrainerId) + 1 : 1;
            _data.Add(vm);
            TempData["Ok"] = "Trenér byl vytvořen.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id:int}")]
        public IActionResult Edit(int id)
        {
            ViewBag.Active = "Admin";
            var t = _data.FirstOrDefault(x => x.TrainerId == id);
            if (t is null) return NotFound();
            return View("~/Views/Trainer/Edit.cshtml", t);
        }

        [HttpPost("Edit/{id:int}"), ValidateAntiForgeryToken]
        public IActionResult EditPost(int id, TrainerViewModel vm)
        {
            if (!ModelState.IsValid) return View("~/Views/Trainer/Edit.cshtml", vm);
            var t = _data.FirstOrDefault(x => x.TrainerId == id);
            if (t is null) return NotFound();

            t.FirstName = vm.FirstName;
            t.LastName = vm.LastName;
            t.Email = vm.Email;
            t.Phone = vm.Phone;
            t.Specialty = vm.Specialty;
            t.Certifications = vm.Certifications;
            t.HourlyRate = vm.HourlyRate;
            t.IsActive = vm.IsActive;

            TempData["Ok"] = "Změny uloženy.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Details/{id:int}")]
        public IActionResult Details(int id)
        {
            ViewBag.Active = "Admin";
            var t = _data.FirstOrDefault(x => x.TrainerId == id);
            if (t is null) return NotFound();
            return View("~/Views/Trainer/Details.cshtml", t);
        }

        [HttpGet("Delete/{id:int}")]
        public IActionResult Delete(int id)
        {
            ViewBag.Active = "Admin";
            var t = _data.FirstOrDefault(x => x.TrainerId == id);
            if (t is null) return NotFound();
            return View("~/Views/Trainer/Delete.cshtml", t);
        }

        [HttpPost("Delete/{id:int}"), ActionName("Delete"), ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var t = _data.FirstOrDefault(x => x.TrainerId == id);
            if (t is not null) _data.Remove(t);
            TempData["Ok"] = "Trenér byl odstraněn.";
            return RedirectToAction(nameof(Index));
        }
    }
}
