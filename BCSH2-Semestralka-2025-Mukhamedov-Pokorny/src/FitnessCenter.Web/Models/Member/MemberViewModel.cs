using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models.Member;

public sealed class MemberViewModel
{
    public int MemberId { get; set; }
    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [Required, StringLength(100)] public string LastName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    public string? Phone { get; set; }
    [DataType(DataType.Date)] public DateTime? BirthDate { get; set; }
    public bool IsActive { get; set; } = true;
}
