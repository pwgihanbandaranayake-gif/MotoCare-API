using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

[Route("api/job-cards")]
public sealed class JobCardsController(IJobCardRepository repo, JobCardService service) : ApiControllerBase
{
    [HttpGet, Permission(AppModules.JobCards, PermissionActions.View)]
    public async Task<IActionResult> List(string? status, string? search) => Ok(await repo.ListAsync(Clean(status), Clean(search)));

    [HttpGet("{id:int}"), Permission(AppModules.JobCards, PermissionActions.View)]
    public async Task<IActionResult> Get(int id) => Ok(await repo.GetAsync(id));

    /// <summary>Completed job cards with no invoice yet (job card dropdown on the invoice screen).</summary>
    [HttpGet("ready-to-bill"), Permission(AppModules.Invoices, PermissionActions.View)]
    public async Task<IActionResult> ReadyToBill() => Ok(await repo.ReadyToBillAsync());

    [HttpPost, Permission(AppModules.JobCards, PermissionActions.Add)]
    public async Task<IActionResult> Save(JobCardSaveDto dto) => Ok(new SavedId(await service.SaveAsync(dto)));

    // ---------- services on the job

    [HttpPost("{id:int}/services"), Permission(AppModules.JobCards, PermissionActions.Edit)]
    public async Task<IActionResult> AddService(int id, JobServiceAddDto dto)
    {
        await service.AddServiceAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("{id:int}/services/{jobServiceId:int}"), Permission(AppModules.JobCards, PermissionActions.Edit)]
    public async Task<IActionResult> RemoveService(int id, int jobServiceId)
    {
        await service.RemoveServiceAsync(id, jobServiceId);
        return NoContent();
    }

    // ---------- parts on the job (stock moves immediately)

    [HttpPost("{id:int}/parts"), Permission(AppModules.JobCards, PermissionActions.Edit)]
    public async Task<IActionResult> IssuePart(int id, JobPartIssueDto dto)
    {
        await service.IssuePartAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("{id:int}/parts/{jobPartId:int}"), Permission(AppModules.JobCards, PermissionActions.Edit)]
    public async Task<IActionResult> RemovePart(int id, int jobPartId)
    {
        await service.RemovePartAsync(id, jobPartId);
        return NoContent();
    }

    // ---------- status: Open / In progress / Completed / Cancelled

    [HttpPut("{id:int}/status"), Permission(AppModules.JobCards, PermissionActions.Edit)]
    public async Task<IActionResult> SetStatus(int id, StatusDto dto)
    {
        await service.SetStatusAsync(id, dto.Status);
        return NoContent();
    }
}
