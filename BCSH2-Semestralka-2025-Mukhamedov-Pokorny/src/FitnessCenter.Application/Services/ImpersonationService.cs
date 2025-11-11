using System.Security.Claims;
using FitnessCenter.Application.Interfaces;
using FitnessCenter.Infrastructure.Repositories; // IMembersRepository
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace FitnessCenter.Application.Services
{
    public class ImpersonationService : IImpersonationService
    {
        private readonly IMembersRepository _members;

        public ImpersonationService(IMembersRepository members)
        {
            _members = members;
        }

        public async Task StartAsync(HttpContext http, string adminId, string targetUserId)
        {
            // uložit původního admina do session
            http.Session.SetString(IImpersonationService.SessionKeyOriginalAdminId, adminId);

            // načíst cílového uživatele z DB
            var idInt = int.Parse(targetUserId);
            var u = await _members.GetByIdAsync(idInt)
                    ?? throw new InvalidOperationException("Uživatel nenalezen.");

            // claims pro cílového uživatele (člena, kterého emuluješ)
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, targetUserId),
                new("MemberId", targetUserId),   // přidáno
                new("UserId", targetUserId),     // přidáno
                new(ClaimTypes.Name, $"{u.FirstName} {u.LastName}"),
                new(ClaimTypes.Role, "Member"),  // nebo "User", podle aplikace
                new(IImpersonationService.IsImpersonatingClaim, "true"),
                new(IImpersonationService.ImpersonatorIdClaim, adminId)
            };

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties { IsPersistent = false });
        }

        public async Task StopAsync(HttpContext http, string? fallbackAdminId = null)
        {
            var adminId = http.Session.GetString(IImpersonationService.SessionKeyOriginalAdminId)
                          ?? fallbackAdminId;

            if (!string.IsNullOrEmpty(adminId))
            {
                // znovu přihlásit admina
                var admin = await _members.GetByIdAsync(int.Parse(adminId));
                if (admin != null)
                {
                    var adminClaims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, adminId),
                        new("MemberId", adminId),   // přidáno
                        new("UserId", adminId),     // přidáno
                        new(ClaimTypes.Name, $"{admin.FirstName} {admin.LastName}"),
                        new(ClaimTypes.Role, "Admin")
                    };

                    var principal = new ClaimsPrincipal(
                        new ClaimsIdentity(adminClaims, CookieAuthenticationDefaults.AuthenticationScheme));

                    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                }
                else
                {
                    // admin už neexistuje – odhlásit
                    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
            else
            {
                // fallback – odhlásit
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            // vyčistit session
            http.Session.Remove(IImpersonationService.SessionKeyOriginalAdminId);
        }
    }
}
