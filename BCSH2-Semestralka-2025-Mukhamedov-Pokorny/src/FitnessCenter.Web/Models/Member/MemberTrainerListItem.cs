using System;
using System.Collections.Generic;

namespace FitnessCenter.Web.Models.Member
{
    public sealed class MemberTrainerListItem
    {
        public int Id { get; set; }
        public string Jmeno { get; set; } = "";
        public string Prijmeni { get; set; } = "";
        public string Telefon { get; set; } = "";

        public string CeleJmeno => $"{Jmeno} {Prijmeni}";
    }

    public sealed class MemberTrainerLessonRow
    {
        public int IdLekce { get; set; }
        public string Nazev { get; set; } = "";
        public DateTime Datum { get; set; }
        public int Obsazenost { get; set; }
    }

    public sealed class MemberTrainerDetailViewModel
    {
        public int TrenerId { get; set; }
        public string Jmeno { get; set; } = "";
        public string Prijmeni { get; set; } = "";
        public string Email { get; set; } = "";
        public string Telefon { get; set; } = "";

        public int PocetLekci { get; set; }

        public List<MemberTrainerLessonRow> Lekce { get; set; } = new();
    }
}
