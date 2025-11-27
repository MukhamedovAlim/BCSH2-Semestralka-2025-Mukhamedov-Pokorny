using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitnessCenter.Web.Models.Member;
using Microsoft.AspNetCore.Http;

public interface IDocumentsRepository
{
    Task<int> InsertMemberDocumentAsync(IFormFile file, int memberId, string uploadedBy);
    Task<IReadOnlyList<MemberDocumentInfo>> GetAllMemberDocumentsAsync();
    Task<MemberDocumentContent?> GetDocumentContentAsync(int documentId);

    Task DeleteDocumentAsync(int documentId);
    Task UpdateDocumentContentAsync(int documentId, IFormFile file, string updatedBy);
}

// DTO pro seznam
public sealed class MemberDocumentInfo
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    public string MemberName { get; set; } = string.Empty;

    public string MemberSurname { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }

    public string UploadedBy { get; set; } = string.Empty;
}

// DTO pro download
namespace FitnessCenter.Web.Models.Member
{
    public sealed class MemberDocumentContent
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();

        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public string Extension { get; set; } = string.Empty;
    }
}
