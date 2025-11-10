using System;
namespace FitnessCenter.Infrastructure.Repositories
{
    public sealed record LogRow(DateTime Kdy, string Tabulka, string Operace, string Kdo, string Popis);
}
