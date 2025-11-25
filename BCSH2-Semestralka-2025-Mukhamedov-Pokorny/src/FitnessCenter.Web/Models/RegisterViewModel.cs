using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Jméno je povinné.")]
        [MaxLength(50, ErrorMessage = "Jméno může mít maximálně 50 znaků.")]
        [RegularExpression(@"^[A-Za-zÀ-ž\s\-]+$", ErrorMessage = "Jméno může obsahovat jen písmena, mezery a pomlčky.")]
        public string FirstName { get; set; } = "";

        [Required(ErrorMessage = "Příjmení je povinné.")]
        [MaxLength(50, ErrorMessage = "Příjmení může mít maximálně 50 znaků.")]
        [RegularExpression(@"^[A-Za-zÀ-ž\s\-]+$", ErrorMessage = "Příjmení může obsahovat jen písmena, mezery a pomlčky.")]
        public string LastName { get; set; } = "";

        [Required(ErrorMessage = "E-mail je povinný.")]
        [EmailAddress(ErrorMessage = "Zadej platný e-mail.")]
        [MaxLength(100, ErrorMessage = "E-mail může mít maximálně 100 znaků.")]
        public string Email { get; set; } = "";

        [MaxLength(20, ErrorMessage = "Telefon může mít maximálně 20 znaků.")]
        [RegularExpression(@"^$|\+?\d{9,15}$", ErrorMessage = "Telefonní číslo není platné.")]
        public string? Phone { get; set; }

        [MaxLength(100, ErrorMessage = "Adresa může mít maximálně 100 znaků.")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Datum narození je povinné.")]
        [DataType(DataType.Date)]
        [CustomValidation(typeof(RegisterViewModel), nameof(ValidateBirthDate))]
        public DateTime? BirthDate { get; set; }

        [Required(ErrorMessage = "Vyber fitness centrum.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vyber fitness centrum.")]
        public int FitnessCenterId { get; set; } = 0;

        [Required(ErrorMessage = "Heslo je povinné.")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Heslo musí mít alespoň 6 znaků.")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Potvrzení hesla je povinné.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Hesla se neshodují.")]
        public string ConfirmPassword { get; set; } = "";

        // ---------- Custom validace data narození ----------
        public static ValidationResult? ValidateBirthDate(DateTime? value, ValidationContext context)
        {
            if (!value.HasValue)
                return new ValidationResult("Datum narození je povinné.");

            var date = value.Value.Date;

            if (date > DateTime.Today)
                return new ValidationResult("Datum narození nesmí být v budoucnosti.");

            // minimální rozumný rok
            if (date < new DateTime(1900, 1, 1))
                return new ValidationResult("Datum narození je příliš staré.");

            // minimálně 15 let
            var minAgeDate = DateTime.Today.AddYears(-15);
            if (date > minAgeDate)
                return new ValidationResult("Musí ti být alespoň 15 let.");

            // maximálně 100 let (ochrana proti nesmyslu)
            var maxAgeDate = DateTime.Today.AddYears(-100);
            if (date < maxAgeDate)
                return new ValidationResult("Zadej reálné datum narození.");

            return ValidationResult.Success;
        }
    }
}
