using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

[Route("api/settings")]
public sealed class SettingsController(ISettingsRepository repo, SettingsService service) : ApiControllerBase
{
    /// <summary>All settings (company name, default customer identification). Readable by every user.</summary>
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await repo.ListAsync());

    [HttpPut("{key}"), Permission(AppModules.Settings, PermissionActions.Edit)]
    public async Task<IActionResult> Save(string key, SettingSaveDto dto)
    {
        await service.SaveAsync(key, dto.Value);
        return NoContent();
    }

    [HttpGet("doc-numbers"), Permission(AppModules.Settings, PermissionActions.View)]
    public async Task<IActionResult> DocNumbers() => Ok(await repo.DocNumbersAsync());

    [HttpPut("doc-numbers/{docType}"), Permission(AppModules.Settings, PermissionActions.Edit)]
    public async Task<IActionResult> SaveDocNumber(string docType, DocNumberDto dto)
    {
        await service.SaveDocNumberAsync(dto with { DocType = docType });
        return NoContent();
    }
}
