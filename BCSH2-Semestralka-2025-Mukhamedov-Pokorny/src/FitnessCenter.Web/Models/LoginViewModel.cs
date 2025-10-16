using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class LoginViewModel
    {
        [Display(Name = "Pøihlašovací jméno")]
        [Required(ErrorMessage = "Vyplòte pøihlašovací jméno.")]
        [StringLength(50)]
        public string UserName { get; set; } = "";

        [Display(Name = "Heslo")]
        [Required(ErrorMessage = "Zadejte heslo.")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Heslo musí mít alespoò {2} znakù.")]
        public string Password { get; set; } = "";
    }
}
