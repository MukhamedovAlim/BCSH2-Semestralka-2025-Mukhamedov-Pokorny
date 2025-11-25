using Microsoft.AspNetCore.Http;

public interface IDocumentsRepository
{
    Task<int> InsertMemberDocumentAsync(IFormFile file, int memberId, string uploadedBy);
}
