namespace FitnessCenter.Web.Models.DBObjects
{
    public sealed class DbObjectDetail
    {
        public string ObjectType { get; set; } = default!;
        public string ObjectName { get; set; } = default!;

        public string? DefinitionText { get; set; }
        public List<TableColumnInfo>? Columns { get; set; }

        public SequenceInfo? Sequence { get; set; }
    }
}
