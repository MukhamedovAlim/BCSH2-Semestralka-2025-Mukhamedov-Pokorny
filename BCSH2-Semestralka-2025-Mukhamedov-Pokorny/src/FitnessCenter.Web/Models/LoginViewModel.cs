using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "E-mail je povinný.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Heslo je povinné.")]
        public string Password { get; set; } = "";
    }
}
