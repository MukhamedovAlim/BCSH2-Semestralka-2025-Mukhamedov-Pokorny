using System;
using System.Collections.Generic;

namespace FitnessCenter.Web.Models
{
    public sealed class LessonDetailDialogViewModel
    {
        public int Id { get; set; }
        public string Nazev { get; set; } = "";
        public DateTime Zacatek { get; set; }
        public string? Mistnost { get; set; }
        public int Kapacita { get; set; }
        public int Reserved { get; set; }

        public string TrainerName { get; set; } = "—";

        //ted se nepuzivaji
        public List<LessonDetailAttendeeRow> Attendees { get; set; } = new();
    }

    public sealed class LessonDetailAttendeeRow
    {
        public string Jmeno { get; set; } = "";
        public string Email { get; set; } = "";
    }
}