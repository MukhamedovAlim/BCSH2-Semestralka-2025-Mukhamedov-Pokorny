namespace FitnessCenter.Web.Models
{
    public class ReservationViewModel
    {
        public int Id { get; set; }
        public string Nazev { get; set; } = "";
        public DateTime Datum { get; set; }
        public int Kapacita { get; set; }
        public int Prihlaseno { get; set; }
        public bool JsemPrihlasen { get; set; }
    }
}
