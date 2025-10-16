using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class LoginViewModel
    {
        [Display(Name = "P�ihla�ovac� jm�no")]
        [Required(ErrorMessage = "Vypl�te p�ihla�ovac� jm�no.")]
        [StringLength(50)]
        public string UserName { get; set; } = "";

        [Display(Name = "Heslo")]
        [Required(ErrorMessage = "Zadejte heslo.")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Heslo mus� m�t alespo� {2} znak�.")]
        public string Password { get; set; } = "";
    }
}
