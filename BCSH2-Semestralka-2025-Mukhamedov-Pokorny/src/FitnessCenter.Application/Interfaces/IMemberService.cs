namespace FitnessCenter.Application.Interfaces;

using FitnessCenter.Domain.Entities;

public interface IMembersService
{
    Task<IReadOnlyList<Member>> GetAllAsync();
    Task<Member?> GetAsync(int id);
    Task<int> CreateAsync(Member m);
    Task<bool> UpdateAsync(Member m);
    Task<bool> DeleteAsync(int id);
}
