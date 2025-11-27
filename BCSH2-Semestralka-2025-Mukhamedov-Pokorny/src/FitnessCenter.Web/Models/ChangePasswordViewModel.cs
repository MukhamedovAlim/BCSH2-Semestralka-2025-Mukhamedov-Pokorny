using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Zadej aktuální heslo.")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "Zadej nové heslo.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "Zopakuj nové heslo.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Hesla se neshodují.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
