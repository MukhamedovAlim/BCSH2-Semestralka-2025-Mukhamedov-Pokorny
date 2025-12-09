using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public sealed class LessonEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Zadej název lekce.")]
        [MaxLength(80)]
        public string Nazev { get; set; } = string.Empty;

        [Required(ErrorMessage = "Zadej datum a čas lekce.")]
        [DataType(DataType.DateTime)]
        public DateTime Zacatek { get; set; }

        [Range(1, 200, ErrorMessage = "Kapacita musí být v rozsahu 1–200.")]
        public int Kapacita { get; set; }

        [MaxLength(200)]
        public string? Popis { get; set; }

        [Required(ErrorMessage = "Vyber fitness centrum.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vyber fitness centrum.")]
        public int? SelectedFitnessCenterId { get; set; }
    }
}
