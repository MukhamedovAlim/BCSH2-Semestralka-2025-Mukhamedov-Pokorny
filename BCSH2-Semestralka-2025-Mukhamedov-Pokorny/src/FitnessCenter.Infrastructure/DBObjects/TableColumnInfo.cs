namespace FitnessCenter.Infrastructure.DBObjects
{
    public sealed class TableColumnInfo
    {
        public int ColumnId { get; set; }
        public string ColumnName { get; set; } = default!;
        public string DataType { get; set; } = default!;
        public int DataLength { get; set; }
        public bool IsNullable { get; set; }
    }
}
