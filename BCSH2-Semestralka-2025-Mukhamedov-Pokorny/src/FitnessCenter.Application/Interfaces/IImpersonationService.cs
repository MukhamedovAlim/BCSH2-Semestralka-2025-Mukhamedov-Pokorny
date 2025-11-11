using FitnessCenter.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace FitnessCenter.Application.Interfaces
{
    public interface IImpersonationService
    {
        Task StartAsync(HttpContext http, string adminId, string targetUserId);
        Task StopAsync(HttpContext http, string? fallbackAdminId = null);

        // standardizované klíče/claimy
        static string ImpersonatorIdClaim => "ImpersonatorId";
        static string IsImpersonatingClaim => "IsImpersonating";
        static string SessionKeyOriginalAdminId => "OriginalAdminId";
    }
}
