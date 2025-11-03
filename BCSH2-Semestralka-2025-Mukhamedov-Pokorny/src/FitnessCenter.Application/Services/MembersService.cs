using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Repositories;

namespace FitnessCenter.Application.Services
{
    public sealed class MembersService : IMembersService
    {
        private readonly IMembersRepository repo;
        public MembersService(IMembersRepository repo) => this.repo = repo;

        public async Task<IReadOnlyList<Member>> GetAllAsync()
            => (await repo.GetAllAsync()).ToList();

        public Task<Member?> GetAsync(int id) => repo.GetByIdAsync(id);
        public Task<int> CreateAsync(Member m) => repo.CreateAsync(m);
        public Task<bool> UpdateAsync(Member m) => repo.UpdateAsync(m);
        public Task<bool> DeleteAsync(int id) => repo.DeleteAsync(id);

        public Task<bool> IsTrainerEmailAsync(string email) => repo.IsTrainerEmailAsync(email);
        public Task<int?> GetTrainerIdByEmailAsync(string email) => repo.GetTrainerIdByEmailAsync(email);

        // admin
        public Task<int> CreateViaProcedureAsync(Member m) => repo.CreateViaProcedureAsync(m);
        public Task UpdateViaProcedureAsync(Member m) => repo.UpdateViaProcedureAsync(m);
    }
}

