using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

[Authorize(Roles = "Member")]
[Route("[controller]")]
public sealed class MemberCertificatesController : Controller
{
    private readonly IDocumentsRepository _documents;

    public MemberCertificatesController(IDocumentsRepository documents)
    {
        _documents = documents;
    }

    [HttpGet("Upload")]
    public IActionResult Upload()
    {
        return View();
    }

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

        var memberIdStr = User.FindFirstValue("MemberId");
        Console.WriteLine($">> Claim MemberId = '{memberIdStr ?? "null"}'");

        if (!int.TryParse(memberIdStr, out var memberId))
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
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            Console.WriteLine("==== CHYBA PRI UPLOADU CERTIFIKATU ====");
            Console.WriteLine(ex);

            TempData["Err"] = "Při nahrávání certifikátu došlo k chybě.";
            return View();
        }
    }
}
