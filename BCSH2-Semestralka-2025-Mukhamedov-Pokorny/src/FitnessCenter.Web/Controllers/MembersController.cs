using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure; // IEmailSender
using FitnessCenter.Infrastructure.Persistence;
using FitnessCenter.Web.Infrastructure.Security;
using FitnessCenter.Web.Models;
using FitnessCenter.Web.Models.Member;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using System.IO;
using MemberVM = FitnessCenter.Web.Models.Member.MemberViewModel;

namespace FitnessCenter.Web.Controllers
{
    // všichni přihlášení; role se řeší na jednotlivých akcích
    [Authorize]
    public class MembersController : Controller
    {
        private readonly IMembersService _members;
        private readonly IEmailSender _emailSender;
        private readonly PasswordHasher<Member> _hasher = new();
        private readonly IWebHostEnvironment _env;   // kvůli ukládání Profilovek

        public MembersController(
            IMembersService members,
            IEmailSender emailSender,
            IWebHostEnvironment env)
        {
            _members = members;
            _emailSender = emailSender;
            _env = env;
        }

        // -------------------------
        // Pomocné metody
        // -------------------------

        private static string GeneratePassword()
        {
            // jednoduché 8-znakové heslo
            return Guid.NewGuid().ToString("N")[..8];
        }

        private static int ReadOutInt(OracleParameter p)
        {
            var v = p?.Value;
            if (v is null) return 0;

            if (v is OracleDecimal od && !od.IsNull)
                return (int)od.Value;

            if (v is decimal dec) return (int)dec;
            if (v is int i) return i;
            if (v is long l) return (int)l;

            return int.TryParse(v.ToString(), out var parsed) ? parsed : 0;
        }

        private static async Task<List<SelectListItem>> LoadFitnessForSelectAsync()
        {
            var items = new List<SelectListItem>();

            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(
                "SELECT idfitness, nazev FROM fitnesscentra ORDER BY nazev",
                (OracleConnection)conn);

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                items.Add(new SelectListItem
                {
                    Value = rd.GetInt32(0).ToString(),
                    Text = rd.GetString(1)
                });
            }
            return items;
        }

        // ===== pomocné pro profilovky =====

        private string GetProfilePicsFolder()
        {
            return Path.Combine(_env.WebRootPath, "profile-pics");
        }

        private string? FindProfilePhotoFile(int memberId)
        {
            var folder = GetProfilePicsFolder();
            if (!Directory.Exists(folder)) return null;

            var pattern = memberId + ".*";
            var matches = Directory.GetFiles(folder, pattern);
            return matches.FirstOrDefault();
        }

        private static string? BuildProfilePhotoUrl(int memberId)
        {
            var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var relPath = Path.Combine("uploads", "avatars", $"member_{memberId}.jpg");
            var fullPath = Path.Combine(wwwroot, relPath);

            if (!System.IO.File.Exists(fullPath))
                return null;

            // cache-buster, aby se po uploadu hned načetla nová fotka
            var url = "/" + relPath.Replace("\\", "/");
            return url + "?v=" + DateTime.UtcNow.Ticks;
        }

        // ======================
        //        LIST (ADMIN)
        // ======================

        // jen pro admina – health check
        [Authorize(Roles = "Admin")]
        [HttpGet("/Members/Ping")]
        public IActionResult Ping() => Content("OK");

        [Authorize(Roles = "Admin")]
        [HttpGet("/Members")]
        [HttpGet("/Members/Index")]
        public async Task<IActionResult> Index(
            string? search,
            string? sort,
            string? membership,
            int? fitnessId,
            CancellationToken ct)
        {
            ViewBag.Active = "Members";
            ViewBag.HideMainNav = true;
            ViewBag.Search = search;
            ViewBag.Sort = sort;
            ViewBag.Membership = membership;
            ViewBag.FitnessId = fitnessId ?? 0;

            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();

            var list = new List<MemberVM>();

            try
            {
                using var conn = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand(@"
SELECT
    c.idclen,
    c.jmeno,
    c.prijmeni,
    c.email,
    c.telefon,
    c.datumnarozeni,
    c.adresa,
    c.fitnesscentrum_idfitness,
    f.nazev AS fitko,

    m.zahajeni,
    m.ukonceni,

    CASE WHEN t.idtrener IS NOT NULL THEN 1 ELSE 0 END AS is_trainer
FROM clenove c
LEFT JOIN fitnesscentra f ON f.idfitness = c.fitnesscentrum_idfitness
LEFT JOIN (
    SELECT
        z.clen_idclen,
        z.zahajeni,
        z.ukonceni
    FROM (
        SELECT
            cl.clen_idclen,
            cl.zahajeni,
            cl.ukonceni,
            ROW_NUMBER() OVER(
                PARTITION BY cl.clen_idclen
                ORDER BY cl.zahajeni DESC NULLS LAST
            ) rn
        FROM clenstvi cl
    ) z
    WHERE z.rn = 1
) m ON m.clen_idclen = c.idclen
LEFT JOIN treneri t ON LOWER(t.email) = LOWER(c.email)
ORDER BY c.prijmeni, c.jmeno
", (OracleConnection)conn);

                using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    list.Add(new MemberVM
                    {
                        MemberId = rd.GetInt32(0),
                        FirstName = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        LastName = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        Email = rd.IsDBNull(3) ? "" : rd.GetString(3),
                        Phone = rd.IsDBNull(4) ? null : rd.GetString(4),
                        BirthDate = rd.IsDBNull(5) ? (DateTime?)null : rd.GetDateTime(5),
                        Address = rd.IsDBNull(6) ? null : rd.GetString(6),

                        FitnessId = rd.IsDBNull(7) ? 0 : rd.GetInt32(7),
                        FitnessName = rd.IsDBNull(8) ? "" : rd.GetString(8),

                        MembershipFrom = rd.IsDBNull(9) ? (DateTime?)null : rd.GetDateTime(9),
                        MembershipTo = rd.IsDBNull(10) ? (DateTime?)null : rd.GetDateTime(10),

                        IsTrainer = !rd.IsDBNull(11) && rd.GetInt32(11) == 1
                    });
                }
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Nepodařilo se načíst seznam členů: " + ex.Message;
                return View(new List<MemberVM>());
            }

            // SEARCH
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                list = list.Where(m =>
                    ($"{m.FirstName} {m.LastName}").ToLower().Contains(s)
                ).ToList();
            }

            // MEMBERSHIP FILTER
            if (!string.IsNullOrWhiteSpace(membership))
            {
                switch (membership.ToLower())
                {
                    case "active":
                        list = list.Where(m => m.HasActiveMembership).ToList();
                        break;

                    case "inactive":
                        list = list.Where(m => !m.HasActiveMembership).ToList();
                        break;
                }
            }

            // FITNESS FILTER
            if (fitnessId.HasValue && fitnessId.Value > 0)
            {
                list = list.Where(m => m.FitnessId == fitnessId.Value).ToList();
            }

            // SORT
            list = sort switch
            {
                "az" => list.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToList(),
                "za" => list.OrderByDescending(m => m.LastName).ThenByDescending(m => m.FirstName).ToList(),
                _ => list.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToList()
            };

            return View(list);
        }

        // =====================================
        //            CREATE (ADMIN)
        // =====================================

        [Authorize(Roles = "Admin")]
        [HttpGet("/Members/Create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
            return View(new MemberCreateViewModel());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("/Members/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MemberCreateViewModel vm)
        {
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();

            if (!vm.BirthDate.HasValue)
                ModelState.AddModelError(nameof(vm.BirthDate), "Zadej datum narození.");

            if (vm.FitnessCenterId <= 0)
                ModelState.AddModelError(nameof(vm.FitnessCenterId), "Vyber fitness centrum.");

            if (!ModelState.IsValid)
                return View(vm);

            var member = new Member
            {
                FirstName = vm.FirstName?.Trim() ?? "",
                LastName = vm.LastName?.Trim() ?? "",
                Email = vm.Email?.Trim() ?? "",
                Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim(),
                Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim(),
                BirthDate = vm.BirthDate!.Value,
                FitnessCenterId = vm.FitnessCenterId,
                MustChangePassword = true
            };

            var plainPassword = GeneratePassword();
            member.PasswordHash = _hasher.HashPassword(member, plainPassword);

            try
            {
                await _members.CreateViaProcedureAsync(member);

                if (!string.IsNullOrWhiteSpace(member.Email))
                {
                    try
                    {
                        var subject = "Váš účet ve Svalovna Gym";
                        var body = $@"
<p>Dobrý den, {member.FirstName} {member.LastName},</p>
<p>byl pro vás vytvořen účet ve <b>Svalovna Gym</b>.</p>
<p>
<b>Přihlašovací e-mail:</b> {member.Email}<br/>
<b>Heslo:</b> {plainPassword}
</p>
<p>Po prvním přihlášení si prosím heslo změňte.</p>";

                        await _emailSender.SendEmailAsync(
                            member.Email,
                            subject,
                            body,
                            isHtml: true
                        );
                    }
                    catch
                    {
                        // mail fail nesmí shodit celý proces
                    }
                }

                TempData["Ok"] = $"Člen byl úspěšně vytvořen. Heslo: {plainPassword}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Chyba při vytváření člena: " + ex.Message;
                return View(vm);
            }
        }

        // =====================================
        //            EDIT (ADMIN)
        // =====================================

        [Authorize(Roles = "Admin")]
        [HttpGet("/Members/Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();

            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(@"
        SELECT
            c.idclen,
            c.jmeno,
            c.prijmeni,
            c.email,
            c.telefon,
            c.datumnarozeni,
            c.adresa,
            c.fitnesscentrum_idfitness
        FROM clenove c
        WHERE c.idclen = :id
    ", (OracleConnection)conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;

            using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync())
            {
                TempData["Err"] = "Člen s daným ID neexistuje.";
                return RedirectToAction(nameof(Index));
            }

            var vm = new MemberEditViewModel
            {
                IdClen = rd.GetInt32(0),
                Jmeno = rd.IsDBNull(1) ? null : rd.GetString(1),
                Prijmeni = rd.IsDBNull(2) ? null : rd.GetString(2),
                Email = rd.IsDBNull(3) ? null : rd.GetString(3),
                Telefon = rd.IsDBNull(4) ? null : rd.GetString(4),
                DatumNarozeni = rd.IsDBNull(5) ? (DateTime?)null : rd.GetDateTime(5),
                Adresa = rd.IsDBNull(6) ? null : rd.GetString(6),
                FitnesscentrumId = rd.IsDBNull(7) ? (int?)null : rd.GetInt32(7)
            };

            return View(vm);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("/Members/Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MemberEditViewModel vm)
        {
            if (vm.IdClen <= 0) vm.IdClen = id;
            if (vm.IdClen != id)
                ModelState.AddModelError(nameof(vm.IdClen), "Nesouhlasí ID v adrese a ve formuláři.");

            if (string.IsNullOrWhiteSpace(vm.Jmeno))
                ModelState.AddModelError(nameof(vm.Jmeno), "Zadej jméno.");

            if (string.IsNullOrWhiteSpace(vm.Prijmeni))
                ModelState.AddModelError(nameof(vm.Prijmeni), "Zadej příjmení.");

            if (string.IsNullOrWhiteSpace(vm.Email))
                ModelState.AddModelError(nameof(vm.Email), "Zadej e-mail.");

            if (!ModelState.IsValid)
            {
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                return View(vm);
            }

            try
            {
                using var conn = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand("PR_CLEN_UPDATE", (OracleConnection)conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };

                cmd.Parameters.Add("p_idclen", OracleDbType.Int32).Value = vm.IdClen;

                cmd.Parameters.Add("p_jmeno", OracleDbType.Varchar2).Value =
                    string.IsNullOrWhiteSpace(vm.Jmeno) ? (object)DBNull.Value : vm.Jmeno.Trim();

                cmd.Parameters.Add("p_prijmeni", OracleDbType.Varchar2).Value =
                    string.IsNullOrWhiteSpace(vm.Prijmeni) ? (object)DBNull.Value : vm.Prijmeni.Trim();

                cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value =
                    string.IsNullOrWhiteSpace(vm.Email) ? (object)DBNull.Value : vm.Email.Trim();

                cmd.Parameters.Add("p_telefon", OracleDbType.Varchar2).Value =
                    string.IsNullOrWhiteSpace(vm.Telefon) ? (object)DBNull.Value : vm.Telefon.Trim();

                cmd.Parameters.Add("p_datumnarozeni", OracleDbType.Date).Value =
                    vm.DatumNarozeni.HasValue ? (object)vm.DatumNarozeni.Value : DBNull.Value;

                cmd.Parameters.Add("p_adresa", OracleDbType.Varchar2).Value =
                    string.IsNullOrWhiteSpace(vm.Adresa) ? (object)DBNull.Value : vm.Adresa.Trim();

                cmd.Parameters.Add("p_idfitness", OracleDbType.Int32).Value =
                    vm.FitnesscentrumId.HasValue ? (object)vm.FitnesscentrumId.Value : DBNull.Value;

                await cmd.ExecuteNonQueryAsync();

                TempData["Ok"] = "Člen byl úspěšně upraven.";
                return RedirectToAction(nameof(Index));
            }
            catch (OracleException ex) when (ex.Number == 20010)
            {
                TempData["Err"] = "Člen s daným ID neexistuje.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.FitnessCenters = await LoadFitnessForSelectAsync();
                ModelState.AddModelError(string.Empty, "Chyba databáze: " + ex.Message);
                return View(vm);
            }
        }

        // =============================
        //            DELETE (ADMIN)
        // =============================

        [Authorize(Roles = "Admin")]
        [HttpPost("/Members/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromForm] int id)
        {
            if (id <= 0)
            {
                TempData["Err"] = "Neplatné ID člena.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var conn = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand("PROC_DELETE_MEMBER", (OracleConnection)conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };

                cmd.Parameters.Add("p_idclen", OracleDbType.Int32).Value = id;

                var pRelek = new OracleParameter("p_del_relekci", OracleDbType.Decimal) { Direction = ParameterDirection.Output };
                var pRez = new OracleParameter("p_del_rezervace", OracleDbType.Decimal) { Direction = ParameterDirection.Output };
                var pClen = new OracleParameter("p_del_clenstvi", OracleDbType.Decimal) { Direction = ParameterDirection.Output };
                var pPlat = new OracleParameter("p_del_platby", OracleDbType.Decimal) { Direction = ParameterDirection.Output };
                var pDok = new OracleParameter("p_del_dokumenty", OracleDbType.Decimal) { Direction = ParameterDirection.Output };
                var pClenRow = new OracleParameter("p_del_clen", OracleDbType.Decimal) { Direction = ParameterDirection.Output };

                cmd.Parameters.Add(pRelek);
                cmd.Parameters.Add(pRez);
                cmd.Parameters.Add(pClen);
                cmd.Parameters.Add(pPlat);
                cmd.Parameters.Add(pDok);
                cmd.Parameters.Add(pClenRow);

                await cmd.ExecuteNonQueryAsync();

                int zRelek = ReadOutInt(pRelek);
                int zRez = ReadOutInt(pRez);
                int zClenstvi = ReadOutInt(pClen);
                int zPlatby = ReadOutInt(pPlat);
                int zDok = ReadOutInt(pDok);
                int zClen = ReadOutInt(pClenRow);

                if (zClen > 0)
                {
                    TempData["Ok"] =
                        $"Člen byl smazán. " +
                        $"rezervace: {zRez}, členství: {zClenstvi}, platby: {zPlatby}, dokumenty: {zDok}.";
                }
                else
                {
                    TempData["Err"] = "Člen s daným ID neexistuje nebo už byl smazán.";
                }
            }
            catch (OracleException ox)
            {
                TempData["Err"] = "Chyba DB při mazání člena: " + ox.Message;
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Neočekávaná chyba při mazání člena: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // =============================
        //            PROFILE (člen)
        // =============================

        [HttpGet("/Members/Profile")]
        public async Task<IActionResult> Profile(CancellationToken ct)
        {
            var memberId = User.GetRequiredCurrentMemberId();
            var me = await _members.GetByIdAsync(memberId);
            if (me == null) return NotFound();

            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            var oconn = (OracleConnection)conn;

            string fitnessName = "";
            if (me.FitnessCenterId > 0)
            {
                using var cmdFit = new OracleCommand(
                    "SELECT nazev FROM fitnesscentra WHERE idfitness = :id",
                    oconn);
                cmdFit.BindByName = true;
                cmdFit.Parameters.Add("id", OracleDbType.Int32).Value = me.FitnessCenterId;

                var result = await cmdFit.ExecuteScalarAsync(ct);
                fitnessName = result == null || result == DBNull.Value ? "" : result.ToString()!;
            }

            var vm = new MemberProfileViewModel
            {
                Jmeno = me.FirstName ?? "",
                Prijmeni = me.LastName ?? "",
                Telefon = me.Phone ?? "",
                Email = me.Email ?? "",
                FitnessCenter = fitnessName,
                ProfilePhotoUrl = BuildProfilePhotoUrl(memberId)
            };

            ViewBag.Active = "Profile";
            return View("Profile", vm);
        }

        [HttpPost("/Members/Profile")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(MemberProfileViewModel model, CancellationToken ct)
        {
            var memberId = User.GetRequiredCurrentMemberId();
            var me = await _members.GetByIdAsync(memberId);
            if (me == null) return NotFound();

            model.Jmeno = me.FirstName ?? "";
            model.Prijmeni = me.LastName ?? "";

            using var connInfo = await DatabaseManager.GetOpenConnectionAsync();
            var oconnInfo = (OracleConnection)connInfo;

            string fitnessName = "";
            if (me.FitnessCenterId > 0)
            {
                using var cmdFit = new OracleCommand(
                    "SELECT nazev FROM fitnesscentra WHERE idfitness = :id",
                    oconnInfo);
                cmdFit.BindByName = true;
                cmdFit.Parameters.Add("id", OracleDbType.Int32).Value = me.FitnessCenterId;
                var result = await cmdFit.ExecuteScalarAsync(ct);
                fitnessName = result == null || result == DBNull.Value ? "" : result.ToString()!;
            }
            model.FitnessCenter = fitnessName;

            // doplníme i profilovku (aby se ukázala i při chybné validaci)
            model.ProfilePhotoUrl = BuildProfilePhotoUrl(memberId);

            // --- ruční jednoduchá validace, ať nespadneme na ORA-01407 ---
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "E-mail je povinný.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Active = "Profile";
                return View("Profile", model);
            }

            var email = model.Email.Trim();
            var telefon = string.IsNullOrWhiteSpace(model.Telefon)
                ? null
                : model.Telefon.Trim();

            using var conn = await DatabaseManager.GetOpenConnectionAsync();
            using var cmd = new OracleCommand(@"
        UPDATE CLENOVE
           SET TELEFON = :tel,
               EMAIL   = :mail
         WHERE IDCLEN = :id", (OracleConnection)conn);
            cmd.BindByName = true;

            cmd.Parameters.Add("tel", OracleDbType.Varchar2).Value =
                telefon == null ? (object)DBNull.Value : telefon;
            cmd.Parameters.Add("mail", OracleDbType.Varchar2).Value = email; // nikdy NULL
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = memberId;

            await cmd.ExecuteNonQueryAsync(ct);

            TempData["Ok"] = "Kontaktní údaje byly upraveny.";
            return RedirectToAction(nameof(Profile));
        }

        // =============================
        //      UPLOAD PROFILOVÉ FOTKY
        // =============================
        [HttpPost("/Members/UploadAvatar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(IFormFile avatar, CancellationToken ct)
        {
            var memberId = User.GetRequiredCurrentMemberId();

            if (avatar == null || avatar.Length == 0)
            {
                TempData["Err"] = "Nebyl vybrán žádný soubor.";
                return RedirectToAction(nameof(Profile));
            }

            // max 2 MB
            if (avatar.Length > 2 * 1024 * 1024)
            {
                TempData["Err"] = "Soubor je příliš velký (max. 2 MB).";
                return RedirectToAction(nameof(Profile));
            }

            var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
            {
                TempData["Err"] = "Podporované jsou pouze soubory JPG a PNG.";
                return RedirectToAction(nameof(Profile));
            }

            var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadDir = Path.Combine(wwwroot, "uploads", "avatars");
            if (!Directory.Exists(uploadDir))
                Directory.CreateDirectory(uploadDir);

            // uložíme jako member_{id}.jpg
            var fileName = $"member_{memberId}.jpg";
            var filePath = Path.Combine(uploadDir, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                await avatar.CopyToAsync(stream, ct);
            }

            // 💡 zkusíme najít trenéra se stejným e-mailem a zkopírujeme fotku i pro něj
            var me = await _members.GetByIdAsync(memberId);
            if (me != null && !string.IsNullOrWhiteSpace(me.Email))
            {
                using var conn = await DatabaseManager.GetOpenConnectionAsync();
                using var cmd = new OracleCommand(
                    "SELECT idtrener FROM treneri WHERE LOWER(email) = LOWER(:mail)",
                    (OracleConnection)conn);

                cmd.BindByName = true;
                cmd.Parameters.Add("mail", OracleDbType.Varchar2).Value = me.Email.Trim();

                var trainerIdObj = await cmd.ExecuteScalarAsync(ct);
                if (trainerIdObj != null && trainerIdObj != DBNull.Value)
                {
                    var trainerId = Convert.ToInt32(trainerIdObj);
                    var trainerFile = Path.Combine(uploadDir, $"trainer_{trainerId}.jpg");
                    System.IO.File.Copy(filePath, trainerFile, overwrite: true);
                }
            }

            TempData["Ok"] = "Profilová fotka byla nahrána.";
            return RedirectToAction(nameof(Profile));
        }

        // =============================
        //  AVATAR PODLE ID ČLENA (pro trenéra / ostatní)
        // =============================
        [HttpGet("/Members/AvatarByMember/{id:int}")]
        public IActionResult AvatarByMember(int id)
        {
            var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadDir = Path.Combine(wwwroot, "uploads", "avatars");
            var memberFile = Path.Combine(uploadDir, $"member_{id}.jpg");

            string pathToUse;
            string contentType;

            if (System.IO.File.Exists(memberFile))
            {
                // člen má nahranou fotku
                pathToUse = memberFile;
                contentType = "image/jpeg";
            }
            else
            {
                // fallback na default avatar
                pathToUse = Path.Combine(wwwroot, "img", "avatar-default.png");
                contentType = "image/png";
            }

            return PhysicalFile(pathToUse, contentType);
        }
    }
}
