using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Repositories; // kvůli LessonAttendee

namespace FitnessCenter.Application.Interfaces
{
    public interface ILessonsService
    {
        Task<IReadOnlyList<Lesson>> GetAllAsync();
        Task<Lesson?> GetAsync(int id);

        Task<int> CreateAsync(Lesson lesson);
        Task<int> CreateAsync(Lesson lesson, int trainerId);

        Task<bool> UpdateAsync(Lesson lesson);
        Task<bool> DeleteAsync(int id);

        Task<IReadOnlyList<Lesson>> GetForTrainerAsync(int trainerId);

        Task<IReadOnlyList<LessonAttendee>> GetAttendeesAsync(int lessonId, CancellationToken ct = default);

        Task RemoveMemberFromLessonAsync(int lessonId, int memberId, int trainerId);

        Task<(int delRelekci, int delRez, int delLekce)> CancelLessonByAdminAsync(int lessonId);
        Task<int> GetTodayCountAsync(CancellationToken ct = default);
    }
}
