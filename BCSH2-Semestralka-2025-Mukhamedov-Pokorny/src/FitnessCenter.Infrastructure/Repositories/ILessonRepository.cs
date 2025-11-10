using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitnessCenter.Domain.Entities;

namespace FitnessCenter.Infrastructure.Repositories
{
    public interface ILessonRepository
    {
        Task<IEnumerable<Lesson>> GetAllAsync(CancellationToken ct = default);
        Task<Lesson?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<int> CreateAsync(Lesson lesson, int trainerId, CancellationToken ct = default);
        Task<bool> UpdateAsync(Lesson lesson, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
        Task<IReadOnlyList<Lesson>> GetForTrainerAsync(int trainerId, CancellationToken ct = default);

        // NOVÉ – seznam e-mailů přihlášených na lekci
        Task<IReadOnlyList<string>> GetAttendeeEmailsAsync(int lessonId, CancellationToken ct = default);

        Task<(int delRelekci, int delRez, int delLekce)> CancelLessonByAdminAsync(int lessonId);

    }
}
