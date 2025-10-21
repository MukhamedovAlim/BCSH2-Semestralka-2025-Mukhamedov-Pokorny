using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class LessonCreateViewModel
    {
        [Required, MaxLength(80)]
        public string Nazev { get; set; } = string.Empty;

        [Required, DataType(DataType.DateTime)]
        public DateTime Zacatek { get; set; } = DateTime.Today.AddHours(18);

        [Required, MaxLength(40)]
        public string Mistnost { get; set; } = "Sál A";

        [Range(1, 200)]
        public int Kapacita { get; set; } = 12;

        [MaxLength(200)]
        public string? Popis { get; set; }
    }
}
