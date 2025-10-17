using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class LoginViewModel
    {
        [Required] public string UserName { get; set; } = string.Empty;
        [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    }
}
