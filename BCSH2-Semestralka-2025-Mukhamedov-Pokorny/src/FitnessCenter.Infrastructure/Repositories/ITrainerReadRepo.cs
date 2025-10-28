using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FitnessCenter.Infrastructure.Repositories
{
    public interface ITrainersReadRepo
    {
        Task<int?> GetTrenerIdByEmailAsync(string email);
    }
}
