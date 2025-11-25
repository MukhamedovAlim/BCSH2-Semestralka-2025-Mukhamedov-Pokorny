using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models.Member
{
    public sealed class MemberCertificateUploadViewModel
    {
        [Required]
        [Display(Name = "Certifikát (soubor)")]
        public IFormFile? File { get; set; }
    }
}

