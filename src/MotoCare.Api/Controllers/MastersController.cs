using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Masters;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

/// <summary>
/// One controller for every simple master file: /api/masters/{entity}
/// entity = payment-types, banks, card-types, vehicle-makes, vehicle-models, services, parts, suppliers, roles ...
/// The permission module comes from the master definition (e.g. parts -> "Parts", banks -> "Masters").
/// </summary>
[Route("api/masters/{entity}")]
public sealed class MastersController(MasterService masters, IPermissionService permissions, ICurrentUser me) : ApiControllerBase
{
    /// <summary>List rows. activeOnly=true is used by dropdowns and is open to every signed-in user.</summary>
    [HttpGet]
    public async Task<IActionResult> List(string entity, string? search, bool activeOnly = false, int? parentId = null, string? filter = null)
    {
        if (!activeOnly && !await Allowed(entity, PermissionActions.View)) return Forbidden();
        return Ok(await masters.ListAsync(entity, search, activeOnly, parentId, filter));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(string entity, int id)
    {
        if (!await Allowed(entity, PermissionActions.View)) return Forbidden();
        var row = await masters.GetAsync(entity, id);
        return row is null ? NotFound(new { message = "Record not found." }) : Ok(row);
    }

    /// <summary>Save: id = 0 adds a new row, id &gt; 0 updates it.</summary>
    [HttpPost]
    public async Task<IActionResult> Save(string entity, [FromBody] JsonElement body)
    {
        bool isNew = !(body.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number && idEl.GetInt32() > 0);
        if (!await Allowed(entity, isNew ? PermissionActions.Add : PermissionActions.Edit)) return Forbidden();
        var id = await masters.SaveAsync(entity, body);

        // user type limits are cached; refresh them after a change
        if (entity.Equals("roles", StringComparison.OrdinalIgnoreCase)) permissions.Invalidate();

        return Ok(new SavedId(id));
    }

    /// <summary>Deletes the row, or deactivates it when it is used by other records.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(string entity, int id)
    {
        if (!await Allowed(entity, PermissionActions.Delete)) return Forbidden();
        return Ok(new { result = await masters.DeleteAsync(entity, id) });
    }

    private Task<bool> Allowed(string entity, string action) =>
        permissions.IsAllowedAsync(me.RoleId, me.RoleName, MasterService.Resolve(entity).Module, action);

    private ObjectResult Forbidden() =>
        StatusCode(403, new { message = "Your user type does not have access to this master file." });
}

/// <summary>Lists the available master files.</summary>
[Route("api/masters")]
public sealed class MasterCatalogController : ApiControllerBase
{
    [HttpGet]
    public IActionResult Catalog() => Ok(MasterRegistry.All.Select(kv => new { key = kv.Key, kv.Value.Table, kv.Value.Module }));
}
