using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models.Lessons
{
    public sealed class CalendarEventCreateViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Vyber datum akce.")]
        [DataType(DataType.Date)]
        [Display(Name = "Datum")]
        public DateTime Date { get; set; }

        [StringLength(80)]
        [Display(Name = "Název akce")]
        public string Type { get; set; } = "";

        [Required(ErrorMessage = "Popis akce je povinný.")]
        [StringLength(400)]
        [Display(Name = "Popis")]
        public string? Description { get; set; }

        public int? FitnessId { get; set; }
    }
}