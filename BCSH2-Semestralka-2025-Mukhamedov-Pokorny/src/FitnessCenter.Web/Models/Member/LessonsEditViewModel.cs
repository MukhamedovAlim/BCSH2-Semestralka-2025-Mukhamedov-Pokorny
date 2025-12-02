using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class LessonEditViewModel
    {
        public int Id { get; set; }

        [Required, MaxLength(80)]
        public string Nazev { get; set; } = string.Empty;

        [Required, DataType(DataType.DateTime)]
        public DateTime Zacatek { get; set; }

        [Range(1, 200)]
        public int Kapacita { get; set; } = 10;

        [MaxLength(200)]
        public string? Popis { get; set; }
    }
}
