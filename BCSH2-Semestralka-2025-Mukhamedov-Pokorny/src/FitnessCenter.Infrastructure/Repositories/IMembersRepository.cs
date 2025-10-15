namespace FitnessCenter.Infrastructure.Repositories;

using FitnessCenter.Domain.Entities;

public interface IMembersRepository
{
    Task<IEnumerable<Member>> GetAllAsync(CancellationToken ct = default);
    Task<Member?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(Member m, CancellationToken ct = default);
    Task<bool> UpdateAsync(Member m, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}