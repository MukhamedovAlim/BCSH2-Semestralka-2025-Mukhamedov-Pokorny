namespace FitnessCenter.Web.Models
{
    public sealed class EquipmentViewModel
    {
        public int Id { get; set; }
        public string Nazev { get; set; } = "";
        public string Typ { get; set; } = "";     // "Kardio" / "Posilovací" / "Volná závaží"
        public string Stav { get; set; } = "OK";  // "OK" / "Oprava" / "Mimo provoz"
        public int FitkoId { get; set; }
        public string Fitko { get; set; } = "";
    }
}
