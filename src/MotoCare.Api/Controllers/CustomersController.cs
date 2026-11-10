using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

[Route("api/customers")]
public sealed class CustomersController(ICustomerRepository repo, CustomerService service, IPermissionService perms, ICurrentUser me)
    : ApiControllerBase
{
    [HttpGet, Permission(AppModules.Customers, PermissionActions.View)]
    public async Task<IActionResult> List(string? search, bool outstandingOnly = false) =>
        Ok(await repo.ListAsync(Clean(search), outstandingOnly));

    [HttpGet("{id:int}"), Permission(AppModules.Customers, PermissionActions.View)]
    public async Task<IActionResult> Get(int id) => Ok(await repo.GetAsync(id));

    /// <summary>Reception search by number plate, name or mobile (at least 2 characters). Open to every user.</summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup(string? term)
    {
        term = Clean(term);
        if (term is null || term.Length < 2) return Ok(new List<RowData>());
        return Ok(await repo.LookupAsync(term));
    }

    /// <summary>Returns the customer already using this plate / mobile (used by the form to warn early).</summary>
    [HttpGet("check-duplicate"), Permission(AppModules.Customers, PermissionActions.View)]
    public async Task<IActionResult> CheckDuplicate(string? regNo, string? mobile, int customerId = 0) =>
        Ok(await repo.CheckDuplicateAsync(Clean(regNo)?.ToUpperInvariant(), Clean(mobile), customerId));

    /// <summary>Add (customerId = 0) needs "Add" permission, edit needs "Edit".</summary>
    [HttpPost]
    public async Task<IActionResult> Save(CustomerSaveDto dto)
    {
        var action = dto.CustomerId == 0 ? PermissionActions.Add : PermissionActions.Edit;
        if (!await perms.IsAllowedAsync(me.RoleId, me.RoleName, AppModules.Customers, action))
            return StatusCode(403, new { message = "Your user type cannot save customers." });

        return Ok(new SavedId(await service.SaveAsync(dto)));
    }
}
