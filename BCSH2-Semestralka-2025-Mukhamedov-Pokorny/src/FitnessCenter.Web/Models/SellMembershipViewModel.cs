using System;
using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models.Admin
{
    public sealed class SellMembershipViewModel
    {
        [Required, EmailAddress, MaxLength(100)]
        public string Email { get; set; } = "";

        [Required, MaxLength(80)]
        public string TypNazev { get; set; } = "";     // např. "Měsíční"

        [Range(0, 1_000_000)]
        public decimal Castka { get; set; }            // cena

        public bool IhnedZaplaceno { get; set; } = true; // checkbox „Zaplaceno“
    }
}
