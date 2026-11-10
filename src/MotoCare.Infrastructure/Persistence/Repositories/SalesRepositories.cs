using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;

namespace MotoCare.Infrastructure.Persistence.Repositories;

// Invoices, receipts, ledgers and stock.

public sealed class InvoiceRepository(DbExecutor db) : IInvoiceRepository
{
    public Task<ResultBag> LoadFromJobAsync(int jobCardId) =>
        db.MultiAsync("dbo.sp_Invoice_LoadFromJob", new { JobCardId = jobCardId }, "job?", "items");

    public Task<ResultBag> GetAsync(int id) =>
        db.MultiAsync("dbo.sp_Invoice_Get", new { Id = id }, "invoice?", "items", "payments");

    public Task<List<RowData>> ListAsync(string? status, DateTime? from, DateTime? to, string? search, int? customerId) =>
        db.ListAsync("dbo.sp_Invoice_List", new { Status = status, From = from?.Date, To = to?.Date, Search = search, CustomerId = customerId });

    public Task<int> SaveAsync(InvoiceSaveModel m) =>
        db.SaveAsync("dbo.sp_Invoice_Save", new
        {
            m.InvoiceId, m.InvoiceDate, m.JobCardId, m.CustomerId,
            m.SubTotal, m.LineDiscount, m.BillDiscountType, m.BillDiscountValue, m.BillDiscountAmt,
            m.TaxId, m.TaxAmt, m.NetTotal, m.Notes, m.UserId,
            Items = TableTypes.InvoiceItems(m.Items)
        });

    public Task DeleteDraftAsync(int invoiceId) =>
        db.ExecAsync("dbo.sp_Invoice_DeleteDraft", new { InvoiceId = invoiceId });

    public Task<RowData?> SettleAsync(SettleModel m) =>
        db.SingleAsync("dbo.sp_Invoice_Settle", new
        {
            m.InvoiceId, m.UserId, m.PaidNow, m.ChangeGiven, CreditDueDate = m.CreditDueDate?.Date,
            Lines = TableTypes.PaymentLines(m.PaidLines)
        });

    public Task VoidAsync(int invoiceId, string reason) =>
        db.ExecAsync("dbo.sp_Invoice_Void", new { InvoiceId = invoiceId, Reason = reason });
}

public sealed class PaymentRepository(DbExecutor db) : IPaymentRepository
{
    public Task<List<RowData>> ListAsync(DateTime? from, DateTime? to, string? search) =>
        db.ListAsync("dbo.sp_Payment_List", new { From = from?.Date, To = to?.Date, Search = search });

    public Task<ResultBag> GetAsync(int id) =>
        db.MultiAsync("dbo.sp_Payment_Get", new { Id = id }, "payment?", "lines");
}

public sealed class LedgerRepository(DbExecutor db) : ILedgerRepository
{
    public Task<ResultBag> CustomerAsync(int customerId, DateTime from, DateTime to) =>
        db.MultiAsync("dbo.sp_CustomerLedger_Get", new { CustomerId = customerId, From = from, To = to }, "summary?", "rows");

    public Task AdjustCustomerAsync(int customerId, decimal debit, decimal credit, string notes) =>
        db.ExecAsync("dbo.sp_CustomerLedger_Adjust", new { CustomerId = customerId, Debit = debit, Credit = credit, Notes = notes });

    public Task<ResultBag> SupplierAsync(int supplierId, DateTime from, DateTime to) =>
        db.MultiAsync("dbo.sp_SupplierLedger_Get", new { SupplierId = supplierId, From = from, To = to }, "summary?", "rows");
}

public sealed class StockRepository(DbExecutor db) : IStockRepository
{
    public Task<RowData?> SummaryAsync() => db.SingleAsync("dbo.sp_Parts_Summary");

    public Task<ResultBag> LedgerAsync(int partId, DateTime? from, DateTime? to) =>
        db.MultiAsync("dbo.sp_StockLedger_Get", new { PartId = partId, From = from?.Date, To = to?.Date }, "part?", "rows");

    public Task AdjustAsync(int partId, decimal qty, string notes) =>
        db.ExecAsync("dbo.sp_Stock_Adjust", new { PartId = partId, Qty = qty, Notes = notes });
}
