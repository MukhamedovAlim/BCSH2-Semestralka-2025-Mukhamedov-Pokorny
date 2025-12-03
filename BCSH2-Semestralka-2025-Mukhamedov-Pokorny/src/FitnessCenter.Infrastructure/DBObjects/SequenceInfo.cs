namespace FitnessCenter.Infrastructure.DBObjects
{
    public sealed class SequenceInfo
    {
        public string SequenceName { get; set; } = default!;
        public decimal MinValue { get; set; }
        public decimal MaxValue { get; set; }
        public decimal IncrementBy { get; set; }
        public decimal LastNumber { get; set; }
    }
}
