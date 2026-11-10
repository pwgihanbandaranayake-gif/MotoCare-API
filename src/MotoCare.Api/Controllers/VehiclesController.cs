using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

[Route("api/vehicles")]
public sealed class VehiclesController(IVehicleRepository repo, VehicleService service) : ApiControllerBase
{
    [HttpGet, Permission(AppModules.Vehicles, PermissionActions.View)]
    public async Task<IActionResult> List(string? search, int? customerId) => Ok(await repo.ListAsync(Clean(search), customerId));

    [HttpGet("{id:int}"), Permission(AppModules.Vehicles, PermissionActions.View)]
    public async Task<IActionResult> Get(int id) => Ok(await repo.GetAsync(id));

    [HttpPost, Permission(AppModules.Vehicles, PermissionActions.Edit)]
    public async Task<IActionResult> Save(VehicleSaveDto dto) => Ok(new SavedId(await service.SaveAsync(dto)));

    // ---------- service reminders

    [HttpGet("reminders"), Permission(AppModules.Vehicles, PermissionActions.View)]
    public async Task<IActionResult> Reminders(string? status, int daysAhead = 30) => Ok(await repo.RemindersAsync(Clean(status), daysAhead));

    [HttpPost("reminders"), Permission(AppModules.Vehicles, PermissionActions.Edit)]
    public async Task<IActionResult> SaveReminder(ReminderSaveDto dto) => Ok(new SavedId(await service.SaveReminderAsync(dto)));

    [HttpPut("reminders/{id:int}/status"), Permission(AppModules.Vehicles, PermissionActions.Edit)]
    public async Task<IActionResult> ReminderStatus(int id, StatusDto dto)
    {
        await service.SetReminderStatusAsync(id, dto.Status);
        return NoContent();
    }
}
