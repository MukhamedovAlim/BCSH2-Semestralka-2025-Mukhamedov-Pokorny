using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class RegisterViewModel
    {
        [Required] public string FirstName { get; set; } = "";
        [Required] public string LastName { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string UserName { get; set; } = "";
        [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
        [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Hesla se neshodují.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
