using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Repositories; // pokud máš ILessonRepository v Infrastructure

namespace FitnessCenter.Application.Services
{
    public sealed class LessonsService : ILessonsService
    {
        private readonly ILessonRepository _repo;

        public LessonsService(ILessonRepository repo)
        {
            _repo = repo;
        }

        public async Task<IReadOnlyList<Lesson>> GetAllAsync()
        {
            var data = await _repo.GetAllAsync();
            return data.OrderBy(x => x.Zacatek).ToList();
        }

        public Task<Lesson?> GetAsync(int id)
            => _repo.GetByIdAsync(id);

        public Task<int> CreateAsync(Lesson lesson)
            => _repo.CreateAsync(lesson);

        public Task<bool> UpdateAsync(Lesson lesson)
            => _repo.UpdateAsync(lesson);

        public Task<bool> DeleteAsync(int id)
            => _repo.DeleteAsync(id);
    }
}
