using BCSH2_Semestralka_2025_Mukhamedov_Pokorny.src.FitnessCenter.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCSH2_Semestralka_2025_Mukhamedov_Pokorny.src.FitnessCenter.Infrastructure.Repositories;
public interface IMembersRepository
{
    Task<IEnumerable<Member>> GetAllAsync(CancellationToken ct = default);
    Task<Member?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(Member m, CancellationToken ct = default);
    Task<bool> UpdateAsync(Member m, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
