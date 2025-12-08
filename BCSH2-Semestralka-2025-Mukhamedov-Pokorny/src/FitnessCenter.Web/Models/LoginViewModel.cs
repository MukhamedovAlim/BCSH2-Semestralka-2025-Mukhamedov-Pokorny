using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Zadej e-mail.")]
        [EmailAddress(ErrorMessage = "Neplatný formát e-mailu.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Zadej heslo.")]
        public string Password { get; set; } = string.Empty;

        // můžeš ho dál používat pro vlastní chybovou hlášku, když chceš
        public string? ErrorMessage { get; set; }
    }
}
