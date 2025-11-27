using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[Authorize(Roles = "Member")]
[Route("MemberCertificates")]
public sealed class MemberCertificatesController : Controller
{
    private readonly IDocumentsRepository _documents;

    public MemberCertificatesController(IDocumentsRepository documents)
    {
        _documents = documents;
    }

    // Pomocná metoda – vytáhne ID člena z claimu
    private bool TryGetCurrentMemberId(out int memberId)
    {
        var memberIdStr = User.FindFirstValue("MemberId");
        return int.TryParse(memberIdStr, out memberId);
    }

    // Přehled certifikátů aktuálního člena
    // GET /MemberCertificates
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (!TryGetCurrentMemberId(out var memberId))
        {
            TempData["Err"] = "Nepodařilo se zjistit ID člena.";
            return RedirectToAction("Index", "Home");
        }

        var allDocs = await _documents.GetAllMemberDocumentsAsync();

        var myDocs = allDocs
            .Where(d => d.MemberId == memberId)
            .OrderByDescending(d => d.UploadedAt)
            .ToList();

        return View(myDocs); // vytvoř si View: Views/MemberCertificates/Index.cshtml
    }

    // Formulář pro upload
    // GET /MemberCertificates/Upload
    [HttpGet("Upload")]
    public IActionResult Upload()
    {
        return View();
    }

    // Zpracování uploadu
    // POST /MemberCertificates/Upload
    [HttpPost("Upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile upload)
    {
        Console.WriteLine("=== POST /MemberCertificates/Upload ===");

        if (upload is null || upload.Length == 0)
        {
            Console.WriteLine(">> ŽÁDNÝ SOUBOR");
            TempData["Err"] = "Musíš vybrat soubor.";
            return View();
        }

        if (!TryGetCurrentMemberId(out var memberId))
        {
            TempData["Err"] = "Nepodařilo se zjistit ID člena.";
            return View();
        }

        var uploadedBy = User.Identity?.Name ?? "unknown";
        Console.WriteLine($">> Upload by = '{uploadedBy}', memberId = {memberId}");

        try
        {
            var newId = await _documents.InsertMemberDocumentAsync(
                upload,
                memberId,
                uploadedBy
            );

            Console.WriteLine($">> dok_vlozit OK, ID_DOK = {newId}");

            TempData["Ok"] = "Certifikát byl nahrán.";
            // Po nahrání ho pošleme na přehled jeho certifikátů
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            Console.WriteLine("==== CHYBA PRI UPLOADU CERTIFIKATU ====");
            Console.WriteLine(ex);

            TempData["Err"] = "Při nahrávání certifikátu došlo k chybě.";
            return View();
        }
    }

    // Stažení vlastního certifikátu
    // GET /MemberCertificates/Download/5
    [HttpGet("Download/{id:int}")]
    public async Task<IActionResult> Download(int id)
    {
        Console.WriteLine($"=== GET /MemberCertificates/Download/{id} ===");

        if (!TryGetCurrentMemberId(out var memberId))
        {
            Console.WriteLine(">> MemberId z claimu se nepodařilo načíst – FORBID");
            return Forbid();
        }

        var allDocs = await _documents.GetAllMemberDocumentsAsync();
        var docInfo = allDocs.FirstOrDefault(d => d.Id == id && d.MemberId == memberId);

        if (docInfo is null)
        {
            Console.WriteLine($">> Dokument {id} pro člena {memberId} nenalezen nebo mu nepatří.");
            return NotFound();
        }

        var doc = await _documents.GetDocumentContentAsync(id);
        if (doc is null)
        {
            Console.WriteLine($">> Obsah dokumentu {id} je NULL – GetDocumentContentAsync vrátil null");
            return NotFound();
        }

        Console.WriteLine($">> Stahuji: {doc.FileName}, typ: {doc.ContentType}, velikost: {doc.Bytes?.Length ?? 0} B");
        return File(doc.Bytes, doc.ContentType, doc.FileName);
    }
}
