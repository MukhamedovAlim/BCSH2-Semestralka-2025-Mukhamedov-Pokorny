namespace FitnessCenter.Infrastructure.Repositories;

using System.Collections.Concurrent;
using FitnessCenter.Domain.Entities;

public sealed class InMemoryMembersRepository : IMembersRepository
{
    private readonly ConcurrentDictionary<int, Member> _store = new();
    private int _seq = 0;

    public InMemoryMembersRepository()
    {
        // seed
        CreateAsync(new Member { FirstName = "Jan", LastName = "Novák", Email = "jan.novak@example.com" }).Wait();
    }

    public Task<IEnumerable<Member>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Values.OrderByDescending(m => m.CreatedAt).AsEnumerable());

    public Task<Member?> GetByIdAsync(int id, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(id, out var m) ? m : null);

    public Task<int> CreateAsync(Member m, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _seq);
        m.MemberId = id;
        m.CreatedAt = DateTime.UtcNow;
        _store[id] = m;
        return Task.FromResult(id);
    }

    public Task<bool> UpdateAsync(Member m, CancellationToken ct = default)
    {
        if (!_store.ContainsKey(m.MemberId)) return Task.FromResult(false);
        _store[m.MemberId] = m;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        => Task.FromResult(_store.TryRemove(id, out _));
}