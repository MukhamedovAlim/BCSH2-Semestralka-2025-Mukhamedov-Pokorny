
namespace FitnessCenter.Domain.Entities;

public class Lesson
{
    public int Id { get; set; }
    public string Nazev { get; set; } = string.Empty;
    public DateTime Zacatek { get; set; }
    public string Mistnost { get; set; } = string.Empty;
    public int Kapacita { get; set; }
    public string? Popis { get; set; }
}

