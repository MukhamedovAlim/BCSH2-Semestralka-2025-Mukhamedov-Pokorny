using FitnessCenter.Domain.Entities;

namespace FitnessCenter.Application.Interfaces
{
    public interface IMembersService
    {
        Task<IReadOnlyList<Member>> GetAllAsync();
        Task<Member?> GetAsync(int id);
        Task<int> CreateAsync(Member m);
        Task<bool> UpdateAsync(Member m);
        Task<bool> DeleteAsync(int id);

        // DOPLNĚNO:
        Task<bool> IsTrainerEmailAsync(string email);
        Task<int?> GetTrainerIdByEmailAsync(string email);
    }
}
