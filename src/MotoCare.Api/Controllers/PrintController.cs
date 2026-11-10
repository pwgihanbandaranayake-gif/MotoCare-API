using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

/// <summary>
/// Print / reprint of bills and documents.
/// The React print page calls POST first (gets the copy number), then prints.
/// </summary>
[Route("api/print")]
public sealed class PrintController(PrintService service, IPrintRepository repo) : ApiControllerBase
{
    /// <summary>Logs a print. Returns { copyNo, printedAt, printedBy }. copyNo 1 = ORIGINAL.</summary>
    [HttpPost("{docType}/{id:int}")]
    public async Task<IActionResult> Log(string docType, int id) => Ok(await service.LogAsync(docType, id));

    /// <summary>Who printed this document and when.</summary>
    [HttpGet("{docType}/{id:int}/history")]
    public async Task<IActionResult> History(string docType, int id) => Ok(await service.HistoryAsync(docType, id));

    /// <summary>Debit note (GRN return) data for printing.</summary>
    [HttpGet("data/debit-note/{id:int}"), Permission(AppModules.Grn, PermissionActions.View)]
    public async Task<IActionResult> DebitNote(int id) => Ok(await repo.DebitNoteAsync(id));

    /// <summary>Supplier payment voucher data for printing.</summary>
    [HttpGet("data/supplier-payment/{id:int}"), Permission(AppModules.SupplierBills, PermissionActions.View)]
    public async Task<IActionResult> SupplierPayment(int id)
    {
        var row = await repo.SupplierPaymentAsync(id);
        return row is null ? NotFound(new { message = "Payment not found." }) : Ok(row);
    }
}
