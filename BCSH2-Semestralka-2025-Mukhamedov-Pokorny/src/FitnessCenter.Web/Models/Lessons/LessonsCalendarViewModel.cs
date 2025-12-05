namespace FitnessCenter.Web.Models.Lessons
{
    public sealed class LessonsCalendarViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }

        //Key = den v měsíci, seznam lekcí v daný den
        public Dictionary<int, List<LessonCalendarItem>> LessonsByDay { get; set; }
            = new();

        //Key = den v měsíci, seznam admin akcí v daný den
        public Dictionary<int, List<CalendarAdminEventItem>> EventsByDay { get; set; }
            = new();
    }
}
