using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers;

public sealed class MembersController(IMembersService service) : Controller
{
    public async Task<IActionResult> Index()
        => View((await service.GetAllAsync()).Select(ToVm));

    private static MemberViewModel ToVm(Member m) => new()
    {
        MemberId = m.MemberId,
        FirstName = m.FirstName,
        LastName = m.LastName,
        Email = m.Email,
        Phone = m.Phone,
        BirthDate = m.BirthDate,
        IsActive = m.IsActive
    };
}
