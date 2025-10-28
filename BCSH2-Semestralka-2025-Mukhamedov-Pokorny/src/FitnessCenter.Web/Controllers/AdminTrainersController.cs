using System.Collections.Generic;
using System.Linq;
using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminTrainersController : Controller
    {
        // Sdílené seedované "úložiště" pro ukázku (zůstává internal static)
        internal static readonly List<TrainerViewModel> Data = new()
        {
            new TrainerViewModel
            {
                TrainerId = 1, FirstName = "Martin", LastName = "Horák",
                Email = "martin.horak@example.com", Phone = "777 888 999",
                Specialty = "Silový trénink", Certifications = "FISAF", HourlyRate = 650, IsActive = true
            },
            new TrainerViewModel
            {
                TrainerId = 2, FirstName = "Lucie", LastName = "Jelínková",
                Email = "lucie.jelinkova@example.com", Phone = "777 222 333",
                Specialty = "Jóga", Certifications = "RYT200", HourlyRate = 550, IsActive = true
            }
        };

        public IActionResult Index()
        {
            ViewBag.Active = "Admin";
            var list = Data.OrderBy(t => t.LastName).ThenBy(t => t.FirstName).ToList();
            return View(list);
        }

        public IActionResult Details(int id)
        {
            var t = Data.FirstOrDefault(x => x.TrainerId == id);
            if (t is null) return NotFound();
            return View(t);
        }

        public IActionResult Create()
        {
            ViewBag.Active = "Admin";
            return View(new TrainerViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Create(TrainerViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            vm.TrainerId = Data.Any() ? Data.Max(x => x.TrainerId) + 1 : 1;
            Data.Add(vm);
            TempData["Ok"] = "Trenér byl vytvořen.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Edit(int id)
        {
            var t = Data.FirstOrDefault(x => x.TrainerId == id);
            if (t is null) return NotFound();
            return View(t);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Edit(int id, TrainerViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var t = Data.FirstOrDefault(x => x.TrainerId == id);
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

        public IActionResult Delete(int id)
        {
            var t = Data.FirstOrDefault(x => x.TrainerId == id);
            if (t is null) return NotFound();
            return View(t);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var t = Data.FirstOrDefault(x => x.TrainerId == id);
            if (t is not null) Data.Remove(t);
            TempData["Ok"] = "Trenér byl odstraněn.";
            return RedirectToAction(nameof(Index));
        }
    }
}
