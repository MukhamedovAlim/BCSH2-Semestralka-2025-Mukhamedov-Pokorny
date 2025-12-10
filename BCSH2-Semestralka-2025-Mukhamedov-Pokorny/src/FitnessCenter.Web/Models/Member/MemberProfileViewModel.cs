using System;
using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models.Member
{
    public class MemberProfileViewModel
    {
        public string Jmeno { get; set; } = "";
        public string Prijmeni { get; set; } = "";

        [Display(Name = "Telefon")]
        public string? Telefon { get; set; }

        [Required(ErrorMessage = "E-mail je povinný.")]
        [EmailAddress(ErrorMessage = "Zadej platný e-mail.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = "";

        public string FitnessCenter { get; set; } = "";

        public string Initials =>
            (string.IsNullOrWhiteSpace(Jmeno) ? "" : Jmeno[0].ToString().ToUpper()) +
            (string.IsNullOrWhiteSpace(Prijmeni) ? "" : Prijmeni[0].ToString().ToUpper());

        // JEDINÁ URL na fotku
        public string? ProfilePhotoUrl { get; set; }
    }
}
