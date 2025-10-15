﻿namespace FitnessCenter.Application.Services;

using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Infrastructure.Repositories;

public sealed class MembersService(IMembersRepository repo) : IMembersService
{
    public async Task<IReadOnlyList<Member>> GetAllAsync() => (await repo.GetAllAsync()).ToList();
    public Task<Member?> GetAsync(int id) => repo.GetByIdAsync(id);
    public Task<int> CreateAsync(Member m) => repo.CreateAsync(m);
    public Task<bool> UpdateAsync(Member m) => repo.UpdateAsync(m);
    public Task<bool> DeleteAsync(int id) => repo.DeleteAsync(id);
}