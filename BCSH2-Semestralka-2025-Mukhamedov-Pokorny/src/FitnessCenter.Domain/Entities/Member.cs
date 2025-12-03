namespace FitnessCenter.Domain.Entities
{
    public sealed class Member
    {
        public int MemberId { get; set; }               // CLENOVE.IDCLEN
        public string FirstName { get; set; } = "";     // JMENO
        public string LastName { get; set; } = "";     // PRIJMENI
        public DateTime BirthDate { get; set; }         // DATUMNAROZENI (NOT NULL v DB)
        public string? Address { get; set; }            // ADRESA (NULL v DB OK)
        public string? Phone { get; set; }              // TELEFON (UNIQUE, NULL povoleno)
        public string Email { get; set; } = "";         // EMAIL (UNIQUE, NOT NULL)
        public string? PasswordHash { get; set; }
        public int FitnessCenterId { get; set; }        // FITNESSCENTRUM_IDFITNESS
        public bool MustChangePassword { get; set; }
    }
}
