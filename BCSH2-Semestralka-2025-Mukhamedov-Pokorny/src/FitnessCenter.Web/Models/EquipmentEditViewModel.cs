using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public sealed class EquipmentEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Zadej název vybavení.")]
        [StringLength(100, ErrorMessage = "Název může mít maximálně 100 znaků.")]
        public string Nazev { get; set; } = "";

        // do DB jde kód 'K' / 'P' / 'V'
        [Required(ErrorMessage = "Vyber typ vybavení.")]
        [RegularExpression("K|P|V", ErrorMessage = "Neplatný typ vybavení.")]
        public string Typ { get; set; } = "K";

        [Required(ErrorMessage = "Vyber stav vybavení.")]
        [StringLength(20, ErrorMessage = "Stav může mít maximálně 20 znaků.")]
        public string Stav { get; set; } = "OK";     // OK / Oprava / Mimo provoz

        [Required(ErrorMessage = "Vyber fitness centrum.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vyber fitness centrum.")]
        public int FitkoId { get; set; }
    }
}
