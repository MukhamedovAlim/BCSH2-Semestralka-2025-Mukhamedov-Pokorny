namespace FitnessCenter.Application.Interfaces;

using FitnessCenter.Domain.Entities;

public interface ILessonsService
{
    Task<IReadOnlyList<Lesson>> GetAllAsync();
    Task<Lesson?> GetAsync(int id);
    Task<int> CreateAsync(Lesson lesson);
    Task<bool> UpdateAsync(Lesson lesson);
    Task<bool> DeleteAsync(int id);
}
