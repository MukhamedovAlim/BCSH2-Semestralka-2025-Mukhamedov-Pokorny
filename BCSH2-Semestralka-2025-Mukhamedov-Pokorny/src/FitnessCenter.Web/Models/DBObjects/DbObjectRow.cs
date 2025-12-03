namespace FitnessCenter.Web.Models.DBObjects
{
    public sealed class DbObjectRow
    {
        public string ObjectType { get; set; } = default!;
        public string ObjectName { get; set; } = default!;
        public DateTime Created { get; set; }
        public DateTime LastDdlTime { get; set; }
    }

}
