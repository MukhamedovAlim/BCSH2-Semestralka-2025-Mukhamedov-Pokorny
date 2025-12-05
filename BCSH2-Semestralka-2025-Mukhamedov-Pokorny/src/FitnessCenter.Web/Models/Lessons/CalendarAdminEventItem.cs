using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models.Lessons
{
    public sealed class CalendarAdminEventItem
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        // akce
        [Required]
        public string Type { get; set; } = "";
        [Required]
        public string Text { get; set; } = "";
        // Volitelné přiřazení ke konkrétnímu fitku
        public int? FitnessId { get; set; }
    }
}
