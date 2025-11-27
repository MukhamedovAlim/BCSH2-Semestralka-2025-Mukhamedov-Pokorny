using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize(Roles = "Admin")]
[Route("AdminMemberCertificates")]
public sealed class AdminMemberCertificatesController : Controller
{
    private readonly IDocumentsRepository _documents;

    public AdminMemberCertificatesController(IDocumentsRepository documents)
    {
        _documents = documents;
    }

    // seznam všech nahraných dokumentů + FILTRACE
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? sort)
    {
        ViewBag.Active = "AdminCertificates";

        var docs = await _documents.GetAllMemberDocumentsAsync();

        // filtr podle jména člena (jméno + příjmení)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();

            docs = docs
                .Where(d =>
                {
                    var first = d.MemberName ?? string.Empty;
                    var last = d.MemberSurname ?? string.Empty;
                    var full = (first + " " + last).Trim();

                    return !string.IsNullOrWhiteSpace(full)
                           && full.Contains(s, StringComparison.CurrentCultureIgnoreCase);
                })
                .ToList();
        }

        // řazení podle data nahrání
        sort = sort?.Trim().ToLowerInvariant();
        if (sort == "newest")
        {
            docs = docs
                .OrderByDescending(d => d.UploadedAt)
                .ToList();
        }
        else if (sort == "oldest")
        {
            docs = docs
                .OrderBy(d => d.UploadedAt)
                .ToList();
        }

        ViewBag.Search = search ?? "";
        ViewBag.Sort = sort ?? "";

        return View(docs.ToList());
    }



    [HttpGet("Download/{id:int}")]
    public async Task<IActionResult> Download(int id)
    {
        var doc = await _documents.GetDocumentContentAsync(id);
        if (doc is null)
            return NotFound();

        return File(doc.Bytes, doc.ContentType, doc.FileName);
    }

    [HttpPost("Delete/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _documents.DeleteDocumentAsync(id);   // volá dok_smazat
            TempData["Ok"] = "Dokument byl smazán.";
        }
        catch (Exception)
        {
            TempData["Err"] = "Při mazání dokumentu došlo k chybě.";
        }

        return RedirectToAction(nameof(Index));
    }


    [HttpGet("Edit/{id:int}")]
    public IActionResult Edit(int id)
    {
        ViewBag.DocId = id;
        return View();
    }

    [HttpPost("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, IFormFile newFile)
    {
        if (newFile == null || newFile.Length == 0)
        {
            TempData["Err"] = "Nebyl vybrán žádný soubor.";
            return RedirectToAction(nameof(Index));
        }

        var updatedBy = User.Identity?.Name ?? "Unknown";

        try
        {
            await _documents.UpdateDocumentContentAsync(id, newFile, updatedBy);
            TempData["Ok"] = "Dokument byl aktualizován.";
        }
        catch (Exception ex)
        {
            Console.WriteLine("==== CHYBA PRI UPRAVE DOKUMENTU ====");
            Console.WriteLine(ex);
            TempData["Err"] = "Při úpravě dokumentu došlo k chybě.";
        }

        return RedirectToAction(nameof(Index));
    }


}
