using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public sealed class EquipmentEditViewModel
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Nazev { get; set; } = "";

        // do DB jde kód 'K' / 'P' / 'V'
        [Required, RegularExpression("K|P|V")]
        public string Typ { get; set; } = "K";

        [Required, MaxLength(20)]
        public string Stav { get; set; } = "OK";     // OK / Oprava / Mimo provoz

        [Range(1, int.MaxValue, ErrorMessage = "Vyber fitness centrum.")]
        public int FitkoId { get; set; }
    }
}
