using System.Security.Claims;

namespace FitnessCenter.Web.Infrastructure.Security
{
    public static class ClaimsExtensions
    {
        /// <summary>
        /// Vrátí ID aktuálního uživatele z claimů (funguje i v emulaci).
        /// Zkouší postupně: "MemberId" -> "UserId" -> ClaimTypes.NameIdentifier.
        /// </summary>
        public static int? GetCurrentMemberIdOrNull(this ClaimsPrincipal user)
        {
            var id =
                user.FindFirst("MemberId")?.Value ??
                user.FindFirst("UserId")?.Value ??
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(id)) return null;
            if (int.TryParse(id, out var parsed)) return parsed;
            return null;
        }

        /// <summary>
        /// Stejné jako výše, ale vyhodí 403, pokud není ID k dispozici.
        /// </summary>
        public static int GetRequiredCurrentMemberId(this ClaimsPrincipal user)
        {
            var id = GetCurrentMemberIdOrNull(user);
            if (!id.HasValue) throw new UnauthorizedAccessException("Chybí identita uživatele.");
            return id.Value;
        }
    }
}
