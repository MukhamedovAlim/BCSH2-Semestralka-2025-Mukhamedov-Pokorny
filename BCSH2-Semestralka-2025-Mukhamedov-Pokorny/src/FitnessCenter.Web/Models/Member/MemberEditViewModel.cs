using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models
{
    public class MemberEditViewModel
    {
        public int MemberId { get; set; }
        [Required, MaxLength(50)] public string FirstName { get; set; } = "";
        [Required, MaxLength(50)] public string LastName { get; set; } = "";
        [Required, EmailAddress, MaxLength(100)] public string Email { get; set; } = "";
        [MaxLength(20)] public string? Phone { get; set; }
        [Required, DataType(DataType.Date)] public DateTime? BirthDate { get; set; }
        [MaxLength(100)] public string? Address { get; set; }
        [Required] public int FitnessCenterId { get; set; } = 1;
    }
}