// MemberEditViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models.Member
{
    public class MemberEditViewModel
    {
        public int IdClen { get; set; }

        [Required(ErrorMessage = "Zadej jméno.")]
        [MaxLength(50)]
        public string? Jmeno { get; set; }

        [Required(ErrorMessage = "Zadej příjmení.")]
        [MaxLength(50)]
        public string? Prijmeni { get; set; }

        [Required(ErrorMessage = "Zadej e-mail.")]
        [MaxLength(100)]
        [RegularExpression(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            ErrorMessage = "Zadej platný e-mail (např. uzivatel@example.cz).")]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? Telefon { get; set; }

        public DateTime? DatumNarozeni { get; set; }

        [MaxLength(100)]
        public string? Adresa { get; set; }

        public int? FitnesscentrumId { get; set; }
    }
}
