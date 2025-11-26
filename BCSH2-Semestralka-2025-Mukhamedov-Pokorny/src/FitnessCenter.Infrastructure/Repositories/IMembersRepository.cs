using FitnessCenter.Domain.Entities;

namespace FitnessCenter.Infrastructure.Repositories
{
    public interface IMembersRepository
    {
        Task<IEnumerable<Member>> GetAllAsync();
        Task<Member?> GetByIdAsync(int id);
        Task<int> CreateAsync(Member m);
        Task<bool> UpdateAsync(Member m);
        Task<bool> DeleteAsync(int id);
        Task<bool> IsTrainerEmailAsync(string email);
        Task<int?> GetTrainerIdByEmailAsync(string email);
        //admin
        Task<int> CreateViaProcedureAsync(Member m);
        Task UpdateViaProcedureAsync(Member m);
    }
}
