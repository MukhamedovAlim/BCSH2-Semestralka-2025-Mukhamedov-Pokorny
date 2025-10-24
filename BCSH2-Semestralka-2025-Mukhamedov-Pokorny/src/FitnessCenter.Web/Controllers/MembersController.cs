using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MembersController : Controller
    {
        // Demo úložiště (nahraď servisem/repozitářem)
        private static readonly List<MemberViewModel> _data =
        [
            new MemberViewModel { MemberId = 1, FirstName = "Jan",  LastName = "Novák",   Email = "jan.novak@example.com",  Phone = "777 111 222", BirthDate = new DateTime(1998,5,2),  IsActive = true },
            new MemberViewModel { MemberId = 2, FirstName = "Eva",  LastName = "Dvořáková", Email = "eva.d@example.com",     Phone = "777 333 444", BirthDate = new DateTime(2000,9,12), IsActive = true },
            new MemberViewModel { MemberId = 3, FirstName = "Petr", LastName = "Král",    Email = "petr.kral@example.com",  Phone = null,          BirthDate = null,                    IsActive = false }
        ];

        // GET: /Members
        public IActionResult Index()
        {
            ViewBag.Active = "Members";
            var list = _data
                .OrderBy(m => m.LastName)
                .ThenBy(m => m.FirstName)
                .ToList();
            return View(list);
        }

        // GET: /Members/Details/5
        public IActionResult Details(int id)
        {
            var m = _data.FirstOrDefault(x => x.MemberId == id);
            if (m is null) return NotFound();
            return View(m);
        }

        // GET: /Members/Create
        public IActionResult Create() => View(new MemberViewModel());

        // POST: /Members/Create
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Create(MemberViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            vm.MemberId = _data.Any() ? _data.Max(x => x.MemberId) + 1 : 1;
            _data.Add(vm);

            TempData["Ok"] = "Člen byl vytvořen.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Members/Edit/5
        public IActionResult Edit(int id)
        {
            var m = _data.FirstOrDefault(x => x.MemberId == id);
            if (m is null) return NotFound();
            return View(m);
        }

        // POST: /Members/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Edit(int id, MemberViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var existing = _data.FirstOrDefault(x => x.MemberId == id);
            if (existing is null) return NotFound();

            existing.FirstName = vm.FirstName;
            existing.LastName = vm.LastName;
            existing.Email = vm.Email;
            existing.Phone = vm.Phone;
            existing.BirthDate = vm.BirthDate;
            existing.IsActive = vm.IsActive;

            TempData["Ok"] = "Změny uloženy.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Members/Delete/5
        public IActionResult Delete(int id)
        {
            var m = _data.FirstOrDefault(x => x.MemberId == id);
            if (m is null) return NotFound();
            return View(m);
        }

        // POST: /Members/Delete/5
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var m = _data.FirstOrDefault(x => x.MemberId == id);
            if (m is not null) _data.Remove(m);
            TempData["Ok"] = "Člen byl odstraněn.";
            return RedirectToAction(nameof(Index));
        }
    }
}
