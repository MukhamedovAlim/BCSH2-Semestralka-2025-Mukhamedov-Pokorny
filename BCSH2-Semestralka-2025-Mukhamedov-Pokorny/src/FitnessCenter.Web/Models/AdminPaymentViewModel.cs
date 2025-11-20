namespace FitnessCenter.Web.Models
{
    public class AdminPaymentViewModel
    {
        public int IdPlatba { get; set; }
        public int MemberId { get; set; }
        public string MemberName { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime Datum { get; set; }
        public decimal Castka { get; set; }
        public string Stav { get; set; } = "";
    }
}

