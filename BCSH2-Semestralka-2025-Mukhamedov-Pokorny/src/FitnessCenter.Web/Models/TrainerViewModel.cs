using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models;

public sealed class TrainerViewModel
{
    public int TrainerId { get; set; }

    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [Required, StringLength(100)] public string LastName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    public string? Phone { get; set; }

    [StringLength(100)] public string? Specialty { get; set; }        // „Jóga“, „Silový trénink“…
    [StringLength(200)] public string? Certifications { get; set; }   // „FISAF, RYT200“
    [Range(0, 5000)] public decimal? HourlyRate { get; set; }       // Kč/h

    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}".Trim();
}
