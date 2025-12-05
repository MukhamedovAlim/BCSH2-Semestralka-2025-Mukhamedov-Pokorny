namespace FitnessCenter.Web.Models.Lessons
{
    public sealed class LessonCalendarItem
    {
        public int Id { get; set; }
        public string Nazev { get; set; } = "";
        public DateTime Zacatek { get; set; }
        public int Kapacita { get; set; }
    }
}