using System.Collections.Generic;
using System.Threading.Tasks;
using FitnessCenter.Domain.Entities;

namespace FitnessCenter.Application.Interfaces
{
    public interface ILessonsService
    {
        Task<IReadOnlyList<Lesson>> GetAllAsync();
        Task<Lesson?> GetAsync(int id);
        Task<int> CreateAsync(Lesson lesson);
        Task<int> CreateAsync(Lesson lesson, int trainerId); //overload
        Task<bool> UpdateAsync(Lesson lesson);
        Task<bool> DeleteAsync(int id);

        Task<IReadOnlyList<Lesson>> GetForTrainerAsync(int trainerId);
    }
}
