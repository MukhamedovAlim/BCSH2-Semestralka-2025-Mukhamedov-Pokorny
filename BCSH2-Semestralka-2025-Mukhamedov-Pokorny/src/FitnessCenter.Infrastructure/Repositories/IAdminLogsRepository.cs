using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitnessCenter.Infrastructure.Repositories
{
    public interface IAdminLogsRepository
    {
        Task<IReadOnlyList<LogRow>> GetLogsAsync(int top = 200);
    }
}
