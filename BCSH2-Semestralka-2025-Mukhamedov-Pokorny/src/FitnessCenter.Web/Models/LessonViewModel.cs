using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class LessonViewModel
    {
        public int Id { get; set; }

        [Required, MaxLength(80)]
        public string Nazev { get; set; } = string.Empty;

        [Required]
        public DateTime Zacatek { get; set; }

        [Required, MaxLength(40)]
        public string Mistnost { get; set; } = string.Empty;

        [Range(1, 200)]
        public int Kapacita { get; set; } = 10;

        [MaxLength(200)]
        public string? Popis { get; set; }
    }
}
