using FitnessCenter.Infrastructure.DBObjects;
using FitnessCenter.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("AdminDbObjects")]
    public sealed class DbCatalogController : Controller
    {
        private readonly DbCatalogRepository _repo;

        public DbCatalogController(DbCatalogRepository repo)
        {
            _repo = repo;
        }

        // /AdminDbObjects
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var objects = await _repo.GetObjectsAsync();
            return View(objects);
        }

        // /AdminDbObjects/DetailPartial?type=TABLE&name=CLENOVE
        [HttpGet("DetailPartial")]
        public async Task<IActionResult> DetailPartial(string type, string name)
        {
            var detail = await _repo.GetDetailAsync(type, name);
            if (detail == null) return NotFound();
            return PartialView("_DbObjectDetailPartial", detail);
        }
    }
}
