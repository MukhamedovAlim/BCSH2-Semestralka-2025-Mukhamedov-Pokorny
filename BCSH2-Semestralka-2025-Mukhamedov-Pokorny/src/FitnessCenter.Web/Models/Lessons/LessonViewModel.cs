using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models.Lessons
{
    public class LessonViewModel
    {
        public int Id { get; set; }

        [Required, MaxLength(80)]
        public string Nazev { get; set; } = string.Empty;

        [Required]
        public DateTime Zacatek { get; set; }

        [Range(1, 200)]
        public int Kapacita { get; set; } = 10;

        [MaxLength(200)]
        public string? Popis { get; set; }
    }
}