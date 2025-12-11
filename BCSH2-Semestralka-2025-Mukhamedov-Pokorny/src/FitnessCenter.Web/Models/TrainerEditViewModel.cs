namespace FitnessCenter.Web.Models
{
    using System.ComponentModel.DataAnnotations;

public class TrainerEditViewModel
{
    public int TrainerId { get; set; }

    [Required(ErrorMessage = "Jméno je povinné.")]
    [StringLength(50, ErrorMessage = "Jméno může mít maximálně 50 znaků.")]
    public string Jmeno { get; set; } = "";

    [Required(ErrorMessage = "Příjmení je povinné.")]
    [StringLength(50, ErrorMessage = "Příjmení může mít maximálně 50 znaků.")]
    public string Prijmeni { get; set; } = "";

    [Required(ErrorMessage = "E-mail je povinný.")]
    [EmailAddress(ErrorMessage = "Zadejte platný e-mail.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Telefon je povinný.")]
    [RegularExpression(@"^\d{9}$", ErrorMessage = "Telefon musí mít 9 číslic bez mezer.")]
    public string Telefon { get; set; } = "";
}

}
