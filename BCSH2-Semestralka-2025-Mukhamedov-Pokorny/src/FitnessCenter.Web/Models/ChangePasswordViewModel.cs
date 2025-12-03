using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Zadej aktuální heslo.")]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "Zadej nové heslo.")]
        [MinLength(6, ErrorMessage = "Heslo musí mít alespoň 6 znaků.")]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "Potvrď nové heslo.")]
        [Compare("NewPassword", ErrorMessage = "Nová hesla se neshodují.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
