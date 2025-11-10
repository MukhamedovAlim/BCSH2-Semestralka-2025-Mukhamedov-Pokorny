namespace FitnessCenter.Web.Models
{
    public class PaymentViewModel
    {
        public DateTime Datum { get; set; }
        public string Popis { get; set; } = "";
        public decimal Castka { get; set; }
        public string Stav { get; set; } = "Zaplaceno"; 
    }
}
