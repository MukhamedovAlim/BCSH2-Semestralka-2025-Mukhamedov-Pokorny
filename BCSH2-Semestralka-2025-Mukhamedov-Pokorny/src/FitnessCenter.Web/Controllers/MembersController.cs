using FitnessCenter.Application.Interfaces;
using FitnessCenter.Domain.Entities;
using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace FitnessCenter.Web.Controllers;

public sealed class MembersController(IMembersService service) : Controller
{
    public async Task<IActionResult> Index()
    {
        var list = await service.GetAllAsync();
        var vm = list.Select(m => new MemberViewModel
        {
            MemberId = m.MemberId,
            FirstName = m.FirstName,
            LastName = m.LastName,
            Email = m.Email,
            Phone = m.Phone,
            BirthDate = m.BirthDate,
            IsActive = m.IsActive
        });
        return View(vm);
    }
}
