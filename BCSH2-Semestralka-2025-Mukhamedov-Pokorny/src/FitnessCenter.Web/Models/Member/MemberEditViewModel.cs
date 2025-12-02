namespace FitnessCenter.Web.Models.Member
{
    public class MemberEditViewModel
    {
        public int IdClen { get; set; }

        public string? Jmeno { get; set; }
        public string? Prijmeni { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }

        public DateTime? DatumNarozeni { get; set; }

        public string? Adresa { get; set; }

        public int? FitnesscentrumId { get; set; }
    }
}
