using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class RegisterViewModel
    {
        [Required] public string FirstName { get; set; } = "";
        [Required] public string LastName { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";

        public string? Address { get; set; }
        public string? Phone { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }   // ?? POVINNÉ pro PR_CLEN_CREATE

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Vyber fitness centrum.")]
        public int FitnessCenterId { get; set; } = 1; // ?? POVINNÉ pro PR_CLEN_CREATE

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required, DataType(DataType.Password),
         Compare(nameof(Password), ErrorMessage = "Hesla se neshodují.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
