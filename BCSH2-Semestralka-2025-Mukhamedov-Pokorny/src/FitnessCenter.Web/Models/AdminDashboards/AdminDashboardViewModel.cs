namespace FitnessCenter.Web.Models.AdminDashboards
{
    public sealed class AdminDashboardViewModel
    {
        public List<string> Mesice { get; set; } = new();
        public List<decimal> Trzby { get; set; } = new();
    }
}
