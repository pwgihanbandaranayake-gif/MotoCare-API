using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

[Route("api/purchase-orders")]
public sealed class PurchaseOrdersController(IPurchasingRepository repo, PurchasingService service) : ApiControllerBase
{
    /// <summary>status: Draft, Approved, Partly received, Received, Closed, Rejected, or Receivable.</summary>
    [HttpGet, Permission(AppModules.PurchaseOrders, PermissionActions.View)]
    public async Task<IActionResult> List(string? status, int? supplierId) => Ok(await repo.PoListAsync(Clean(status), supplierId));

    [HttpGet("{id:int}"), Permission(AppModules.PurchaseOrders, PermissionActions.View)]
    public async Task<IActionResult> Get(int id) => Ok(await repo.PoGetAsync(id));

    /// <summary>Parts at or below reorder level with a suggested quantity.</summary>
    [HttpGet("low-stock"), Permission(AppModules.PurchaseOrders, PermissionActions.View)]
    public async Task<IActionResult> LowStock() => Ok(await repo.LowStockAsync());

    [HttpPost, Permission(AppModules.PurchaseOrders, PermissionActions.Add)]
    public async Task<IActionResult> Save(PurchaseOrderSaveDto dto) => Ok(new SavedId(await service.SavePoAsync(dto)));

    /// <summary>action: APPROVE, REJECT or CLOSE.</summary>
    [HttpPut("{id:int}/status"), Permission(AppModules.PurchaseOrders, PermissionActions.Approve)]
    public async Task<IActionResult> SetStatus(int id, PoActionDto dto)
    {
        await service.SetPoStatusAsync(id, dto.Action);
        return NoContent();
    }
}

[Route("api/grn")]
public sealed class GrnController(IPurchasingRepository repo, PurchasingService service) : ApiControllerBase
{
    [HttpGet, Permission(AppModules.Grn, PermissionActions.View)]
    public async Task<IActionResult> List(string? status, int? supplierId) => Ok(await repo.GrnListAsync(Clean(status), supplierId));

    [HttpGet("{id:int}"), Permission(AppModules.Grn, PermissionActions.View)]
    public async Task<IActionResult> Get(int id) => Ok(await repo.GrnGetAsync(id));

    /// <summary>PO lines still to receive.</summary>
    [HttpGet("pending/{poId:int}"), Permission(AppModules.Grn, PermissionActions.Add)]
    public async Task<IActionResult> Pending(int poId) => Ok(await service.PendingFromPoAsync(poId));

    [HttpPost, Permission(AppModules.Grn, PermissionActions.Add)]
    public async Task<IActionResult> Save(GrnSaveDto dto) => Ok(new SavedId(await service.SaveGrnAsync(dto)));

    /// <summary>Puts the accepted goods into stock.</summary>
    [HttpPost("{id:int}/post"), Permission(AppModules.Grn, PermissionActions.Edit)]
    public async Task<IActionResult> Post(int id)
    {
        await service.PostGrnAsync(id);
        return NoContent();
    }

    [HttpDelete("{id:int}"), Permission(AppModules.Grn, PermissionActions.Edit)]
    public async Task<IActionResult> Delete(int id)
    {
        await service.DeleteGrnAsync(id);
        return NoContent();
    }

    /// <summary>Creates a debit note (return to supplier) for a posted GRN.</summary>
    [HttpPost("{id:int}/returns"), Permission(AppModules.Grn, PermissionActions.Edit)]
    public async Task<IActionResult> Return(int id, GrnReturnDto dto) => Ok(new SavedId(await service.SaveReturnAsync(id, dto)));

    [HttpGet("returns"), Permission(AppModules.Grn, PermissionActions.View)]
    public async Task<IActionResult> Returns() => Ok(await repo.ReturnListAsync());
}

[Route("api/supplier-bills")]
public sealed class SupplierBillsController(ISupplierBillRepository repo, SupplierBillService service) : ApiControllerBase
{
    /// <summary>status: Due, Partial, Paid, or Open (= unpaid).</summary>
    [HttpGet, Permission(AppModules.SupplierBills, PermissionActions.View)]
    public async Task<IActionResult> List(int? supplierId, string? status) => Ok(await repo.ListAsync(supplierId, Clean(status)));

    [HttpGet("unbilled-grn"), Permission(AppModules.SupplierBills, PermissionActions.View)]
    public async Task<IActionResult> Unbilled(int? supplierId) => Ok(await repo.UnbilledGrnAsync(supplierId));

    [HttpPost, Permission(AppModules.SupplierBills, PermissionActions.Add)]
    public async Task<IActionResult> Save(SupplierBillSaveDto dto) => Ok(new SavedId(await service.SaveBillAsync(dto)));

    [HttpPost("payments"), Permission(AppModules.SupplierBills, PermissionActions.Add)]
    public async Task<IActionResult> Pay(SupplierPaymentDto dto) => Ok(new SavedId(await service.PayAsync(dto)));

    [HttpGet("payments"), Permission(AppModules.SupplierBills, PermissionActions.View)]
    public async Task<IActionResult> Payments(int? supplierId) => Ok(await repo.PaymentsAsync(supplierId));
}
