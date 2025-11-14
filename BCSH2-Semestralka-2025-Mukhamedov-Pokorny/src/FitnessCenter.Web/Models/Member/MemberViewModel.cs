using System;
using System.ComponentModel.DataAnnotations;

namespace FitnessCenter.Web.Models.Member
{
    public sealed class MemberViewModel
    {
        public int MemberId { get; set; }

        [MaxLength(50)]
        public string FirstName { get; set; } = "";

        [MaxLength(50)]
        public string LastName { get; set; } = "";

        [EmailAddress, MaxLength(100)]
        public string Email { get; set; } = "";

        [MaxLength(20)]
        public string? Phone { get; set; }

        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [MaxLength(100)]
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;
        public int FitnessId { get; set; }
        [MaxLength(100)]
        public string FitnessName { get; set; } = "";

        public string FullName => $"{FirstName} {LastName}".Trim();

        public bool IsTrainer { get; set; } = false;


        public DateTime? MembershipFrom { get; set; }
        public DateTime? MembershipTo { get; set; }
        public bool HasActiveMembership =>
            MembershipFrom.HasValue && MembershipTo.HasValue &&
            DateTime.Today >= MembershipFrom.Value.Date &&
            DateTime.Today <= MembershipTo.Value.Date;

    }
}
