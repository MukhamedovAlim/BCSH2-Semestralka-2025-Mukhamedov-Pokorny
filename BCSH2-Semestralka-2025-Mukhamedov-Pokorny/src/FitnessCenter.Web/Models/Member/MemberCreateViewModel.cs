// MemberCreateViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class MemberCreateViewModel
    {
        [Required(ErrorMessage = "Zadej jméno."), MaxLength(50)]
        public string FirstName { get; set; } = "";

        [Required(ErrorMessage = "Zadej příjmení."), MaxLength(50)]
        public string LastName { get; set; } = "";

        [Required(ErrorMessage = "Zadej e-mail.")]
        [MaxLength(100)]
        [RegularExpression(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            ErrorMessage = "Zadej platný e-mail (např. uzivatel@example.cz).")]
        public string Email { get; set; } = "";

        [MaxLength(20)]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Zadej datum narození.")]
        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [MaxLength(100)]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Vyber fitness centrum.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vyber fitness centrum.")]
        public int FitnessCenterId { get; set; }
    }
}
