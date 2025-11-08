using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models;

public class TrainerViewModel
{
    public int TrainerId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";

    public string FullName => $"{FirstName} {LastName}".Trim();
}
