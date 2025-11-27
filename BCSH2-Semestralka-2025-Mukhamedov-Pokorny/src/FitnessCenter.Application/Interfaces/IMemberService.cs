using FitnessCenter.Domain.Entities;

namespace FitnessCenter.Application.Interfaces
{
    public interface IMembersService
    {
        Task<IReadOnlyList<Member>> GetAllAsync();
        Task<Member?> GetAsync(int id);
        Task<int> CreateAsync(Member m);
        Task<bool> UpdateAsync(Member m);
        Task ChangePasswordAsync(int memberId, string newPasswordHash);
        Task<bool> DeleteAsync(int id);
        Task<bool> IsTrainerEmailAsync(string email);
        Task<int?> GetTrainerIdByEmailAsync(string email);
        //admin
        Task<int> CreateViaProcedureAsync(Member m);
        Task UpdateViaProcedureAsync(Member m);
        Task<Member?> GetByIdAsync(int id, CancellationToken ct = default);
    }
}
