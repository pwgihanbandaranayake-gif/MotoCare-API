using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

[Route("api/ledgers")]
public sealed class LedgersController(ILedgerRepository repo, LedgerService service) : ApiControllerBase
{
    [HttpGet("customer/{customerId:int}"), Permission(AppModules.Ledgers, PermissionActions.View)]
    public async Task<IActionResult> Customer(int customerId, DateTime? from, DateTime? to)
    {
        var (start, end) = LedgerService.Period(from, to);
        return Ok(await repo.CustomerAsync(customerId, start, end));
    }

    [HttpPost("customer/{customerId:int}/adjust"), Permission(AppModules.Ledgers, PermissionActions.Edit)]
    public async Task<IActionResult> Adjust(int customerId, LedgerAdjustDto dto)
    {
        await service.AdjustCustomerAsync(customerId, dto);
        return NoContent();
    }

    [HttpGet("supplier/{supplierId:int}"), Permission(AppModules.Ledgers, PermissionActions.View)]
    public async Task<IActionResult> Supplier(int supplierId, DateTime? from, DateTime? to)
    {
        var (start, end) = LedgerService.Period(from, to);
        return Ok(await repo.SupplierAsync(supplierId, start, end));
    }
}

[Route("api/stock")]
public sealed class StockController(IStockRepository repo, StockService service) : ApiControllerBase
{
    [HttpGet("summary"), Permission(AppModules.Parts, PermissionActions.View)]
    public async Task<IActionResult> Summary() => Ok(await repo.SummaryAsync());

    [HttpGet("ledger/{partId:int}"), Permission(AppModules.Parts, PermissionActions.View)]
    public async Task<IActionResult> Ledger(int partId, DateTime? from, DateTime? to) => Ok(await repo.LedgerAsync(partId, from, to));

    [HttpPost("adjust"), Permission(AppModules.Parts, PermissionActions.Edit)]
    public async Task<IActionResult> Adjust(StockAdjustDto dto)
    {
        await service.AdjustAsync(dto);
        return NoContent();
    }
}
