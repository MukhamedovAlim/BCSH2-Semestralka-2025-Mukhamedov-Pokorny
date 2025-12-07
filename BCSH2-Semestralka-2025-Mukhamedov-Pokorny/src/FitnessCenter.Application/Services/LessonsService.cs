using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;

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
            var data = await _repo.GetAllAsync(CancellationToken.None);
            return data.OrderBy(x => x.Zacatek).ToList();
        }

        public Task<Lesson?> GetAsync(int id)
            => _repo.GetByIdAsync(id, CancellationToken.None);

        public async Task<int> CreateAsync(Lesson lesson)
        {
            var trainerIdClaim = _http.HttpContext?.User?.FindFirst("TrainerId")?.Value;
            if (!int.TryParse(trainerIdClaim, out var trainerId) || trainerId <= 0)
                throw new InvalidOperationException("TrainerId claim nebyl nalezen.");
            return await _repo.CreateAsync(lesson, trainerId, CancellationToken.None);
        }

        public Task<int> CreateAsync(Lesson lesson, int trainerId)
            => _repo.CreateAsync(lesson, trainerId, CancellationToken.None);

        public Task<bool> UpdateAsync(Lesson lesson)
            => _repo.UpdateAsync(lesson, CancellationToken.None);

        public Task<bool> DeleteAsync(int id)
            => _repo.DeleteAsync(id, CancellationToken.None);

        public async Task<IReadOnlyList<Lesson>> GetForTrainerAsync(int trainerId)
        {
            var list = await _repo.GetForTrainerAsync(trainerId, CancellationToken.None);
            return list.OrderBy(x => x.Zacatek).ToList();
        }

        public Task<IReadOnlyList<LessonAttendee>> GetAttendeesAsync(int lessonId, CancellationToken ct = default)
            => _repo.GetAttendeesAsync(lessonId, ct);

        public Task RemoveMemberFromLessonAsync(int lessonId, int memberId, int trainerId)
            => _repo.RemoveMemberFromLessonAsync(lessonId, memberId, trainerId);

        public Task<(int delRelekci, int delRez, int delLekce)> CancelLessonByAdminAsync(int lessonId)
            => _repo.CancelLessonByAdminAsync(lessonId);

        public Task<int> GetTodayCountAsync(CancellationToken ct = default)
            => _repo.GetTodayCountAsync(ct);
    }
}
