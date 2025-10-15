using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models;

public sealed class MemberViewModel
{
    public int MemberId { get; set; }

    [Required, StringLength(100)]
    public string FirstName { get; set; } = "";

    [Required, StringLength(100)]
    public string LastName { get; set; } = "";

    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = "";

    [Phone]
    public string? Phone { get; set; }

    [DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }

    public bool IsActive { get; set; } = true;
}
