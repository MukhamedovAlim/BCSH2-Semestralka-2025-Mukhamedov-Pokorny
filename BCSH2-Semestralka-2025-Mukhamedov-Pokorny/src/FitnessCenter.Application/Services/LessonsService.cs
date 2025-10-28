using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace FitnessCenter.Application.Services
{
    public sealed class LessonsService : ILessonsService
    {
        private readonly ILessonRepository _repo;
        private readonly IHttpContextAccessor _http;

        public LessonsService(ILessonRepository repo, IHttpContextAccessor http)
        {
            _repo = repo;
            _http = http;
        }

        public async Task<IReadOnlyList<Lesson>> GetAllAsync()
        {
            var data = await _repo.GetAllAsync();
            return data.OrderBy(x => x.Zacatek).ToList();
        }

        public Task<Lesson?> GetAsync(int id) => _repo.GetByIdAsync(id);

        public async Task<int> CreateAsync(Lesson lesson)
        {
            var trainerIdClaim = _http.HttpContext?.User?.FindFirst("TrainerId")?.Value;
            if (!int.TryParse(trainerIdClaim, out var trainerId) || trainerId <= 0)
                throw new InvalidOperationException("TrainerId claim nebyl nalezen.");
            return await _repo.CreateAsync(lesson, trainerId, CancellationToken.None);
        }

        // overload
        public Task<int> CreateAsync(Lesson lesson, int trainerId)
            => _repo.CreateAsync(lesson, trainerId, CancellationToken.None);

        public Task<bool> UpdateAsync(Lesson lesson)
            => _repo.UpdateAsync(lesson, CancellationToken.None);

        public Task<bool> DeleteAsync(int id)
            => _repo.DeleteAsync(id, CancellationToken.None);

        public async Task<IReadOnlyList<Lesson>> GetForTrainerAsync(int trainerId)
        {
            // nejlepší: použít proceduru s filtrem (už ji máš)
            var list = await _repo.GetForTrainerAsync(trainerId, CancellationToken.None);
            return list.OrderBy(x => x.Zacatek).ToList();
            // Alternativa, kdybys proceduru neměl:
            // return (await _repo.GetAllAsync()).Where(x => x.TrainerId == trainerId).OrderBy(x => x.Zacatek).ToList();
        }
    }
}
