using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Zadej e-mail.")]
        [EmailAddress(ErrorMessage = "Neplatný formát e-mailu.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;
    }
}