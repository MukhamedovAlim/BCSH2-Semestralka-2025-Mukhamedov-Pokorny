using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class ChangePasswordViewModel
    {
        // Staré heslo NEvyžadujeme při resetu – necháme bez [Required]
        public string? CurrentPassword { get; set; }

        [Required(ErrorMessage = "Zadej nové heslo.")]
        [MinLength(6, ErrorMessage = "Heslo musí mít alespoň 6 znaků.")]
        [Display(Name = "Nové heslo")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Potvrď nové heslo.")]
        [Display(Name = "Potvrzení nového hesla")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Nová hesla se neshodují.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public bool FromReset { get; set; } = true;
    }
}
