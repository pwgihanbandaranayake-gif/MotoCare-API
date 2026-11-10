using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

// =====================================================================
// Added: part search, quotations, stock counting, payroll, messages, PDF download.
// GET actions read from the repository; changes go through a service that validates first.
// =====================================================================

/// <summary>Type-ahead part search used by every "add part" box (any signed-in user).</summary>
[Route("api/parts")]
public sealed class PartsController(IPartRepository repo) : ApiControllerBase
{
    /// <summary>Example: GET /api/parts/search?term=brake&amp;makeId=1&amp;modelId=3</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(string? term, int? makeId, int? modelId, int? categoryId, int top = 30, bool activeOnly = true) =>
        Ok(await repo.SearchAsync(Clean(term), makeId is > 0 ? makeId : null, modelId is > 0 ? modelId : null,
                                  categoryId is > 0 ? categoryId : null, activeOnly, Math.Clamp(top, 1, 1000)));
}

[Route("api/quotations")]
public sealed class QuotationsController(IQuotationRepository repo, QuotationService service) : ApiControllerBase
{
    [HttpGet, Permission(AppModules.Quotations, PermissionActions.View)]
    public async Task<IActionResult> List(string? status, DateTime? from, DateTime? to, string? search) =>
        Ok(await repo.ListAsync(Clean(status), from, to, Clean(search)));

    [HttpGet("{id:int}"), Permission(AppModules.Quotations, PermissionActions.View)]
    public async Task<IActionResult> Get(int id) => Ok(await repo.GetAsync(id));

    [HttpPost, Permission(AppModules.Quotations, PermissionActions.Add)]
    public async Task<IActionResult> Save(QuotationSaveDto dto) => Ok(new SavedId(await service.SaveAsync(dto)));

    /// <summary>Open / Sent / Accepted / Rejected / Converted (with invoiceId).</summary>
    [HttpPut("{id:int}/status"), Permission(AppModules.Quotations, PermissionActions.Edit)]
    public async Task<IActionResult> SetStatus(int id, QuotationStatusDto dto) { await service.SetStatusAsync(id, dto); return NoContent(); }

    [HttpDelete("{id:int}"), Permission(AppModules.Quotations, PermissionActions.Edit)]
    public async Task<IActionResult> Delete(int id) { await service.DeleteAsync(id); return NoContent(); }
}

[Route("api/stock-counts")]
public sealed class StockCountsController(IStockCountRepository repo, StockCountService service) : ApiControllerBase
{
    [HttpGet, Permission(AppModules.Parts, PermissionActions.View)]
    public async Task<IActionResult> List() => Ok(await repo.ListAsync());

    [HttpGet("{id:int}"), Permission(AppModules.Parts, PermissionActions.View)]
    public async Task<IActionResult> Get(int id) => Ok(await repo.GetAsync(id));

    /// <summary>Starts a count sheet (snapshot of the system quantity).</summary>
    [HttpPost, Permission(AppModules.Parts, PermissionActions.Edit)]
    public async Task<IActionResult> Create(StockCountCreateDto dto) => Ok(new SavedId(await service.CreateAsync(dto)));

    [HttpPut("{id:int}/counts"), Permission(AppModules.Parts, PermissionActions.Edit)]
    public async Task<IActionResult> SaveCounts(int id, List<StockCountLineDto> lines) { await service.SaveCountsAsync(id, lines); return NoContent(); }

    /// <summary>Adjusts stock to the counted quantities.</summary>
    [HttpPost("{id:int}/post"), Permission(AppModules.Parts, PermissionActions.Approve)]
    public async Task<IActionResult> Post(int id) { await service.PostAsync(id); return NoContent(); }

    [HttpDelete("{id:int}"), Permission(AppModules.Parts, PermissionActions.Edit)]
    public async Task<IActionResult> Delete(int id) { await service.DeleteAsync(id); return NoContent(); }
}

[Route("api/payroll")]
public sealed class PayrollController(IPayrollRepository repo, PayrollService service) : ApiControllerBase
{
    // ---------- salary setup
    [HttpGet("profiles"), Permission(AppModules.Payroll, PermissionActions.View)]
    public async Task<IActionResult> Profiles() => Ok(await repo.ProfilesAsync());

    [HttpGet("profiles/{employeeId:int}"), Permission(AppModules.Payroll, PermissionActions.View)]
    public async Task<IActionResult> Profile(int employeeId) => Ok(await repo.ProfileAsync(employeeId));

    [HttpPut("profiles/{employeeId:int}"), Permission(AppModules.Payroll, PermissionActions.Edit)]
    public async Task<IActionResult> SaveProfile(int employeeId, PayrollProfileDto dto) { await service.SaveProfileAsync(employeeId, dto); return NoContent(); }

    // ---------- payroll runs
    [HttpGet("runs"), Permission(AppModules.Payroll, PermissionActions.View)]
    public async Task<IActionResult> Runs() => Ok(await repo.RunsAsync());

    [HttpGet("runs/{id:int}"), Permission(AppModules.Payroll, PermissionActions.View)]
    public async Task<IActionResult> Run(int id) => Ok(await repo.RunAsync(id));

    /// <summary>Calculates salaries for the date range and saves a Draft run.</summary>
    [HttpPost("runs"), Permission(AppModules.Payroll, PermissionActions.Add)]
    public async Task<IActionResult> Create(PayrollRunDto dto) => Ok(new SavedId(await service.CreateRunAsync(dto)));

    [HttpPost("runs/{id:int}/recalculate"), Permission(AppModules.Payroll, PermissionActions.Edit)]
    public async Task<IActionResult> Recalculate(int id) { await service.RecalculateAsync(id); return NoContent(); }

    /// <summary>Extra allowance / deduction. employeeId empty = same amount for everyone in the run.</summary>
    [HttpPost("runs/{id:int}/extras"), Permission(AppModules.Payroll, PermissionActions.Edit)]
    public async Task<IActionResult> AddExtra(int id, PayrollExtraDto dto) { await service.AddExtraAsync(id, dto); return NoContent(); }

    [HttpDelete("runs/{id:int}/extras/{extraId:int}"), Permission(AppModules.Payroll, PermissionActions.Edit)]
    public async Task<IActionResult> DeleteExtra(int id, int extraId) { await service.DeleteExtraAsync(id, extraId); return NoContent(); }

    /// <summary>Approved / Paid / Draft (reopen).</summary>
    [HttpPut("runs/{id:int}/status"), Permission(AppModules.Payroll, PermissionActions.Approve)]
    public async Task<IActionResult> SetStatus(int id, StatusDto dto) { await service.SetStatusAsync(id, dto.Status); return NoContent(); }

    [HttpDelete("runs/{id:int}"), Permission(AppModules.Payroll, PermissionActions.Edit)]
    public async Task<IActionResult> DeleteRun(int id) { await service.DeleteRunAsync(id); return NoContent(); }

    // ---------- Added: advances / expenses (deducted in the next payroll run)
    /// <summary>status: Outstanding / Deducted / empty = all</summary>
    [HttpGet("advances"), Permission(AppModules.Payroll, PermissionActions.View)]
    public async Task<IActionResult> Advances(int? employeeId, string? status, DateTime? from, DateTime? to) =>
        Ok(await repo.AdvancesAsync(employeeId is > 0 ? employeeId : null, Clean(status), from, to));

    [HttpPost("advances"), Permission(AppModules.Payroll, PermissionActions.Add)]
    public async Task<IActionResult> SaveAdvance(AdvanceSaveDto dto) => Ok(new SavedId(await service.SaveAdvanceAsync(dto)));

    [HttpDelete("advances/{id:int}"), Permission(AppModules.Payroll, PermissionActions.Edit)]
    public async Task<IActionResult> DeleteAdvance(int id) { await service.DeleteAdvanceAsync(id); return NoContent(); }

    [HttpGet("payslips/{lineId:int}"), Permission(AppModules.Payroll, PermissionActions.View)]
    public async Task<IActionResult> Payslip(int lineId) => Ok(await repo.PayslipAsync(lineId));
}

[Route("api/messages")]
public sealed class MessagesController(IMessageRepository repo, MessageService service, IPermissionService permissions, ICurrentUser me)
    : ApiControllerBase
{
    /// <summary>Outbox: every e-mail / WhatsApp message and its status.</summary>
    [HttpGet, Permission(AppModules.Messages, PermissionActions.View)]
    public async Task<IActionResult> List(string? status, string? docType, DateTime? from, DateTime? to) =>
        Ok(await repo.ListAsync(Clean(status), Clean(docType), from, to));

    /// <summary>Name, e-mail, mobile and preferred channel of a document's customer / employee.</summary>
    [HttpGet("contact/{docType}/{id:int}")]
    public async Task<IActionResult> Contact(string docType, int id)
    {
        if (!await CanSeeDocAsync(docType)) return Forbidden();
        return Ok(await service.ContactAsync(docType, id));
    }

    /// <summary>Send a document now by e-mail and / or WhatsApp.</summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send(SendDocumentDto dto)
    {
        if (!await CanSeeDocAsync(dto.DocType)) return Forbidden();
        return Ok(await service.SendNowAsync(dto));
    }

    [HttpPost("{id:int}/retry"), Permission(AppModules.Messages, PermissionActions.Edit)]
    public async Task<IActionResult> Retry(int id) { await service.RetryAsync(id); return NoContent(); }

    /// <summary>Send all pending messages now (instead of waiting for the background sender).</summary>
    [HttpPost("process"), Permission(AppModules.Messages, PermissionActions.Edit)]
    public async Task<IActionResult> Process() { await service.ProcessPendingAsync(100); return NoContent(); }

    private async Task<bool> CanSeeDocAsync(string? docType) =>
        await permissions.IsAllowedAsync(me.RoleId, me.RoleName, ModuleOf(docType), PermissionActions.View);

    internal static string ModuleOf(string? docType) => (docType ?? "").ToUpperInvariant() switch
    {
        "PAYSLIP" => AppModules.Payroll,
        "QUOTATION" => AppModules.Quotations,
        _ => AppModules.Invoices
    };

    private ObjectResult Forbidden() => StatusCode(403, new { message = "You do not have access to this document." });
}

/// <summary>Download a bill / payslip / quotation as PDF (same file that is e-mailed / sent on WhatsApp).</summary>
[Route("api/documents")]
public sealed class DocumentsController(IDocumentPdfBuilder pdf, IPermissionService permissions, ICurrentUser me) : ApiControllerBase
{
    [HttpGet("{docType}/{id:int}/pdf")]
    public async Task<IActionResult> Pdf(string docType, int id)
    {
        string type = docType.ToUpperInvariant();
        if (!await permissions.IsAllowedAsync(me.RoleId, me.RoleName, MessagesController.ModuleOf(type), PermissionActions.View))
            return StatusCode(403, new { message = "You do not have access to this document." });
        var file = await pdf.BuildAsync(type, id);
        return File(file.Content, "application/pdf", file.FileName);
    }
}
