using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class RegisterViewModel
    {
        [Display(Name = "Jméno")]
        [Required, StringLength(50)]
        public string FirstName { get; set; } = "";

        [Display(Name = "Pøíjmení")]
        [Required, StringLength(50)]
        public string LastName { get; set; } = "";

        [Display(Name = "E-mail")]
        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; } = "";

        [Display(Name = "Pøihlašovací jméno")]
        [Required, StringLength(50)]
        public string UserName { get; set; } = "";

        [Display(Name = "Heslo")]
        [Required, DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = "";

        [Display(Name = "Potvrzení hesla")]
        [Required, DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Hesla se neshodují.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
