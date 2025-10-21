using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitnessCenter.Domain.Entities;

namespace FitnessCenter.Infrastructure.Repositories
{
    public sealed class InMemoryLessonsRepository : ILessonRepository
    {
        private readonly List<Lesson> _data = new()
        {
            new Lesson { Id = 1, Nazev = "Crossfit", Zacatek = DateTime.Today.AddDays(1).AddHours(18), Mistnost = "Sál A", Kapacita = 12, Popis = "Intenzivní" },
            new Lesson { Id = 2, Nazev = "Jóga",     Zacatek = DateTime.Today.AddDays(2).AddHours(17), Mistnost = "Sál B", Kapacita = 20 }
        };

        public Task<IEnumerable<Lesson>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IEnumerable<Lesson>>(_data.OrderBy(x => x.Zacatek).ToList());

        public Task<Lesson?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(_data.FirstOrDefault(x => x.Id == id));

        public Task<int> CreateAsync(Lesson lesson, CancellationToken ct = default)
        {
            lesson.Id = _data.Count == 0 ? 1 : _data.Max(x => x.Id) + 1;
            _data.Add(lesson);
            return Task.FromResult(lesson.Id);
        }

        public Task<bool> UpdateAsync(Lesson lesson, CancellationToken ct = default)
        {
            var i = _data.FindIndex(x => x.Id == lesson.Id);
            if (i < 0) return Task.FromResult(false);
            _data[i] = lesson;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var removed = _data.RemoveAll(x => x.Id == id) > 0;
            return Task.FromResult(removed);
        }
    }
}
