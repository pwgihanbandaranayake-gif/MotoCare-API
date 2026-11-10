using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

[Route("api/invoices")]
public sealed class InvoicesController(IInvoiceRepository repo, InvoiceService service) : ApiControllerBase
{
    /// <summary>status: Draft, Paid, Partial, Credit, Void, or Open (= has a balance).</summary>
    [HttpGet, Permission(AppModules.Invoices, PermissionActions.View)]
    public async Task<IActionResult> List(string? status, DateTime? from, DateTime? to, string? search, int? customerId) =>
        Ok(await repo.ListAsync(Clean(status), from, to, Clean(search), customerId));

    [HttpGet("{id:int}"), Permission(AppModules.Invoices, PermissionActions.View)]
    public async Task<IActionResult> Get(int id) => Ok(await repo.GetAsync(id));

    /// <summary>Every service and part of a completed job card, with prices.</summary>
    [HttpGet("from-job/{jobCardId:int}"), Permission(AppModules.Invoices, PermissionActions.Add)]
    public async Task<IActionResult> FromJob(int jobCardId) => Ok(await service.LoadFromJobAsync(jobCardId));

    /// <summary>Creates or updates a draft invoice.</summary>
    [HttpPost, Permission(AppModules.Invoices, PermissionActions.Add)]
    public async Task<IActionResult> Save(InvoiceSaveDto dto) => Ok(new SavedId(await service.SaveAsync(dto)));

    [HttpDelete("{id:int}"), Permission(AppModules.Invoices, PermissionActions.Edit)]
    public async Task<IActionResult> DeleteDraft(int id)
    {
        await service.DeleteDraftAsync(id);
        return NoContent();
    }

    /// <summary>Settle with one or more payment lines. Call again later to receive payment on the balance.</summary>
    [HttpPost("{id:int}/settle"), Permission(AppModules.Payments, PermissionActions.Add)]
    public async Task<IActionResult> Settle(int id, SettleDto dto) => Ok(await service.SettleAsync(id, dto));

    [HttpPost("{id:int}/void"), Permission(AppModules.Invoices, PermissionActions.Edit)]
    public async Task<IActionResult> Void(int id, VoidDto dto)
    {
        await service.VoidAsync(id, dto.Reason);
        return NoContent();
    }
}

[Route("api/payments")]
public sealed class PaymentsController(IPaymentRepository repo) : ApiControllerBase
{
    [HttpGet, Permission(AppModules.Payments, PermissionActions.View)]
    public async Task<IActionResult> List(DateTime? from, DateTime? to, string? search) => Ok(await repo.ListAsync(from, to, Clean(search)));

    [HttpGet("{id:int}"), Permission(AppModules.Payments, PermissionActions.View)]
    public async Task<IActionResult> Get(int id) => Ok(await repo.GetAsync(id));
}
