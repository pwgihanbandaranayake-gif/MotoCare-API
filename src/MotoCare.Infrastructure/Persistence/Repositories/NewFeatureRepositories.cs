using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;

namespace MotoCare.Infrastructure.Persistence.Repositories;

// Added: part search, quotations, stock counting, payroll and messages. One stored procedure per method.

public sealed class PartRepository(DbExecutor db) : IPartRepository
{
    public Task<List<RowData>> SearchAsync(string? term, int? makeId, int? modelId, int? categoryId, bool activeOnly, int top) =>
        db.ListAsync("dbo.sp_Part_Search", new { Term = term, MakeId = makeId, ModelId = modelId, CategoryId = categoryId, ActiveOnly = activeOnly, Top = top });
}

public sealed class QuotationRepository(DbExecutor db) : IQuotationRepository
{
    public Task<List<RowData>> ListAsync(string? status, DateTime? from, DateTime? to, string? search) =>
        db.ListAsync("dbo.sp_Quotation_List", new { Status = status, From = from?.Date, To = to?.Date, Search = search });

    public Task<ResultBag> GetAsync(int id) => db.MultiAsync("dbo.sp_Quotation_Get", new { Id = id }, "quotation?", "items");

    public Task<int> SaveAsync(QuotationSaveDto d, InvoiceTotalsModel t, List<InvoiceItemDto> items, int userId) =>
        db.SaveAsync("dbo.sp_Quotation_Save", new
        {
            d.QuotationId, QuoteDate = d.QuoteDate.Date, ValidUntil = d.ValidUntil?.Date, d.CustomerId, d.CustomerName, d.Mobile, d.Email,
            d.VehicleId, d.VehicleText, t.SubTotal, t.LineDiscount, t.BillDiscountType, t.BillDiscountValue, t.BillDiscountAmt,
            d.TaxId, t.TaxAmt, t.NetTotal, d.Notes, UserId = userId,
            Items = TableTypes.InvoiceItems(items)
        });

    public Task SetStatusAsync(int id, string status, int? invoiceId) =>
        db.ExecAsync("dbo.sp_Quotation_SetStatus", new { QuotationId = id, Status = status, InvoiceId = invoiceId });

    public Task DeleteAsync(int id) => db.ExecAsync("dbo.sp_Quotation_Delete", new { QuotationId = id });
}

public sealed class StockCountRepository(DbExecutor db) : IStockCountRepository
{
    public Task<List<RowData>> ListAsync() => db.ListAsync("dbo.sp_StockCount_List");

    public Task<ResultBag> GetAsync(int id) => db.MultiAsync("dbo.sp_StockCount_Get", new { Id = id }, "count?", "items");

    public Task<int> CreateAsync(StockCountCreateDto d, int userId) =>
        db.SaveAsync("dbo.sp_StockCount_Create", new { CountDate = d.CountDate.Date, d.CategoryId, d.MakeId, d.Notes, UserId = userId });

    public Task SaveCountsAsync(int countId, List<StockCountLineDto> lines) =>
        db.ExecAsync("dbo.sp_StockCount_SaveCounts", new { CountId = countId, Items = TableTypes.StockCountItems(lines) });

    public Task PostAsync(int countId, int userId) => db.ExecAsync("dbo.sp_StockCount_Post", new { CountId = countId, UserId = userId });

    public Task DeleteAsync(int countId) => db.ExecAsync("dbo.sp_StockCount_Delete", new { CountId = countId });
}

public sealed class PayrollRepository(DbExecutor db) : IPayrollRepository
{
    public Task<List<RowData>> ProfilesAsync() => db.ListAsync("dbo.sp_PayrollProfile_List");

    public Task<ResultBag> ProfileAsync(int employeeId) =>
        db.MultiAsync("dbo.sp_PayrollProfile_Get", new { EmployeeId = employeeId }, "profile?", "services", "allowances");

    public Task SaveProfileAsync(int employeeId, PayrollProfileDto d, List<ServiceCommissionDto> commissions, List<EmployeeAllowanceDto> allowances) =>
        db.ExecAsync("dbo.sp_PayrollProfile_Save", new
        {
            EmployeeId = employeeId, d.BasicSalary, d.DefaultCommissionPct, d.IsOnPayroll,
            Commissions = TableTypes.ServiceCommissions(commissions),
            Allowances = TableTypes.EmployeeAllowances(allowances)
        });

    public Task<ResultBag> InputsAsync(DateTime from, DateTime to, int? runId) =>
        db.MultiAsync("dbo.sp_Payroll_Inputs", new { From = from.Date, To = to.Date, RunId = runId }, "employees", "allowances", "commissions", "advances");

    public Task<int> SaveRunAsync(PayrollRunModel r) =>
        db.SaveAsync("dbo.sp_Payroll_Run_Save", new
        {
            r.RunId, FromDate = r.FromDate.Date, ToDate = r.ToDate.Date, r.PayFullBasic, r.EpfEnabled,
            r.EpfEmployeePct, r.EpfEmployerPct, r.EtfPct, r.Notes, r.UserId,
            Lines = TableTypes.PayrollLines(r.Lines),
            Details = TableTypes.PayrollDetails(r.Details)
        });

    public Task<List<RowData>> RunsAsync() => db.ListAsync("dbo.sp_Payroll_Run_List");

    public Task<ResultBag> RunAsync(int runId) => db.MultiAsync("dbo.sp_Payroll_Run_Get", new { Id = runId }, "run?", "lines", "extras", "advances");

    public Task AddExtraAsync(int runId, PayrollExtraDto d) =>
        db.ExecAsync("dbo.sp_Payroll_Extra_Add", new { RunId = runId, d.EmployeeId, d.Description, d.Amount, d.IsDeduction, d.EpfApplicable });

    public Task DeleteExtraAsync(int extraId) => db.ExecAsync("dbo.sp_Payroll_Extra_Delete", new { ExtraId = extraId });

    public Task SetStatusAsync(int runId, string status, int userId) =>
        db.ExecAsync("dbo.sp_Payroll_Run_SetStatus", new { RunId = runId, Status = status, UserId = userId });

    public Task DeleteRunAsync(int runId) => db.ExecAsync("dbo.sp_Payroll_Run_Delete", new { RunId = runId });

    public Task<ResultBag> PayslipAsync(int lineId) =>
        db.MultiAsync("dbo.sp_Payslip_Get", new { LineId = lineId }, "slip?", "details", "extras", "advances");

    // ---- Added: employee advances / expenses
    public Task<List<RowData>> AdvancesAsync(int? employeeId, string? status, DateTime? from, DateTime? to) =>
        db.ListAsync("dbo.sp_Advance_List", new { EmployeeId = employeeId, Status = status, From = from?.Date, To = to?.Date });

    public Task<RowData?> AdvanceGetAsync(int id) => db.SingleAsync("dbo.sp_Advance_Get", new { Id = id });

    public Task<int> SaveAdvanceAsync(AdvanceSaveDto d, int userId) =>
        db.SaveAsync("dbo.sp_Advance_Save", new
        {
            d.AdvanceId, d.EmployeeId, AdvanceDate = d.AdvanceDate.Date, d.AdvanceType, d.Description, d.Amount, UserId = userId
        });

    public Task DeleteAdvanceAsync(int id) => db.ExecAsync("dbo.sp_Advance_Delete", new { Id = id });
}

public sealed class MessageRepository(DbExecutor db) : IMessageRepository
{
    public Task<int> QueueAsync(string docType, int docId, string? docNo, string channel, string recipient, string? recipientName, int? userId) =>
        db.SaveAsync("dbo.sp_Message_Queue", new
        {
            DocType = docType, DocId = docId, DocNo = docNo, Channel = channel, Recipient = recipient, RecipientName = recipientName, UserId = userId
        });

    public Task<List<RowData>> ListAsync(string? status, string? docType, DateTime? from, DateTime? to) =>
        db.ListAsync("dbo.sp_Message_List", new { Status = status, DocType = docType, From = from?.Date, To = to?.Date });

    public Task<List<RowData>> PendingAsync(int max) => db.ListAsync("dbo.sp_Message_Pending", new { Max = max });

    public Task SetResultAsync(int messageId, string status, string? error) =>
        db.ExecAsync("dbo.sp_Message_SetResult", new { MessageId = messageId, Status = status, Error = error });

    public Task RetryAsync(int messageId) => db.ExecAsync("dbo.sp_Message_Retry", new { MessageId = messageId });

    public Task<RowData?> ContactAsync(string docType, int docId) =>
        db.SingleAsync("dbo.sp_Message_Contact", new { DocType = docType, DocId = docId });
}
