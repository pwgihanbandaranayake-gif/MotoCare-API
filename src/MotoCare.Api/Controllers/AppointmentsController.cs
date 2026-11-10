using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

[Route("api/appointments")]
public sealed class AppointmentsController(IAppointmentRepository repo, AppointmentService service) : ApiControllerBase
{
    /// <summary>Appointments between two dates (default: today).</summary>
    [HttpGet, Permission(AppModules.Appointments, PermissionActions.View)]
    public async Task<IActionResult> List(DateTime? from, DateTime? to, string? status)
    {
        var start = from ?? DateTime.Today;
        var end = to ?? start;
        return Ok(await repo.ListAsync(start, end, Clean(status)));
    }

    [HttpPost, Permission(AppModules.Appointments, PermissionActions.Add)]
    public async Task<IActionResult> Save(AppointmentSaveDto dto) => Ok(new SavedId(await service.SaveAsync(dto)));

    [HttpPut("{id:int}/status"), Permission(AppModules.Appointments, PermissionActions.Edit)]
    public async Task<IActionResult> SetStatus(int id, StatusDto dto)
    {
        await service.SetStatusAsync(id, dto.Status);
        return NoContent();
    }

    /// <summary>Checks the vehicle in and opens a job card. Returns the new job card id.</summary>
    [HttpPost("{id:int}/check-in"), Permission(AppModules.JobCards, PermissionActions.Add)]
    public async Task<IActionResult> CheckIn(int id, CheckInDto dto) => Ok(new SavedId(await service.CheckInAsync(id, dto)));
}
