using System.Security.Claims;
using FitnessCenter.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers
{
    [Route("AdminImpersonation")]
    public class AdminImpersonationController : Controller
    {
        private readonly IImpersonationService _impersonation;

        public AdminImpersonationController(IImpersonationService impersonation)
        {
            _impersonation = impersonation;
        }

        // volitelný test
        [HttpGet("Ping")]
        [Authorize(Roles = "Admin")]
        public IActionResult Ping() => Content("OK");

        // START emulace – smí jen admin
        [HttpPost("Start")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start([FromForm] string userId, [FromForm] string? returnUrl = "/")
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? throw new InvalidOperationException("Admin není přihlášen (chybí NameIdentifier).");

            await _impersonation.StartAsync(HttpContext, adminId, userId);
            return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
        }

        // STOP emulace – musí fungovat i v roli Member
        [HttpPost("Stop")]
        [Authorize] // stačí být přihlášen (aktuálně jsi ten emulovaný uživatel)
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Stop([FromForm] string? returnUrl = "/")
        {
            await _impersonation.StopAsync(HttpContext);
            return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
        }
    }
}
