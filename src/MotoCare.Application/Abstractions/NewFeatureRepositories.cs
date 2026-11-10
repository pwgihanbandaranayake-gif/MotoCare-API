using MotoCare.Application.Dtos;

namespace MotoCare.Application.Abstractions;

// =====================================================================
// Added: repositories and outside services for the new features.
// =====================================================================

public interface IPartRepository
{
    /// <summary>Type-ahead part search. Make / model / category are optional filters.</summary>
    Task<List<RowData>> SearchAsync(string? term, int? makeId, int? modelId, int? categoryId, bool activeOnly, int top);
}

public interface IQuotationRepository
{
    Task<List<RowData>> ListAsync(string? status, DateTime? from, DateTime? to, string? search);
    Task<ResultBag> GetAsync(int id);                                            // quotation?, items
    Task<int> SaveAsync(QuotationSaveDto dto, InvoiceTotalsModel totals, List<InvoiceItemDto> items, int userId);
    Task SetStatusAsync(int id, string status, int? invoiceId);
    Task DeleteAsync(int id);
}

/// <summary>Totals passed to SQL (calculated in C#).</summary>
public sealed record InvoiceTotalsModel(decimal SubTotal, decimal LineDiscount, string BillDiscountType, decimal BillDiscountValue,
                                        decimal BillDiscountAmt, decimal TaxAmt, decimal NetTotal);

public interface IStockCountRepository
{
    Task<List<RowData>> ListAsync();
    Task<ResultBag> GetAsync(int id);                                            // count?, items
    Task<int> CreateAsync(StockCountCreateDto dto, int userId);
    Task SaveCountsAsync(int countId, List<StockCountLineDto> lines);
    Task PostAsync(int countId, int userId);
    Task DeleteAsync(int countId);
}

public interface IPayrollRepository
{
    Task<List<RowData>> ProfilesAsync();
    Task<ResultBag> ProfileAsync(int employeeId);                                // profile?, services, allowances
    Task SaveProfileAsync(int employeeId, PayrollProfileDto dto, List<ServiceCommissionDto> commissions, List<EmployeeAllowanceDto> allowances);
    Task<ResultBag> InputsAsync(DateTime from, DateTime to, int? runId);        // employees, allowances, commissions, advances (Changed: + runId)
    Task<int> SaveRunAsync(PayrollRunModel run);
    Task<List<RowData>> RunsAsync();
    Task<ResultBag> RunAsync(int runId);                                         // run?, lines, extras, advances
    Task AddExtraAsync(int runId, PayrollExtraDto dto);
    Task DeleteExtraAsync(int extraId);
    Task SetStatusAsync(int runId, string status, int userId);
    Task DeleteRunAsync(int runId);
    Task<ResultBag> PayslipAsync(int lineId);                                    // slip?, details, extras, advances

    // Added: employee advances / expenses
    Task<List<RowData>> AdvancesAsync(int? employeeId, string? status, DateTime? from, DateTime? to);
    Task<RowData?> AdvanceGetAsync(int id);
    Task<int> SaveAdvanceAsync(AdvanceSaveDto dto, int userId);
    Task DeleteAdvanceAsync(int id);
}

public interface IMessageRepository
{
    Task<int> QueueAsync(string docType, int docId, string? docNo, string channel, string recipient, string? recipientName, int? userId);
    Task<List<RowData>> ListAsync(string? status, string? docType, DateTime? from, DateTime? to);
    Task<List<RowData>> PendingAsync(int max);
    Task SetResultAsync(int messageId, string status, string? error);
    Task RetryAsync(int messageId);
    Task<RowData?> ContactAsync(string docType, int docId);                       // DocNo, Name, Email, Mobile, SendVia
}

// ---------- outside services (implemented in Infrastructure) ----------

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string to, string toName, string subject, string body, PdfFile attachment);
}

public interface IWhatsAppSender
{
    bool IsConfigured { get; }
    Task SendDocumentAsync(string mobile, string recipientName, string caption, PdfFile document);
}

/// <summary>Builds the PDF of a bill / payslip / quotation for e-mail and WhatsApp.</summary>
public interface IDocumentPdfBuilder
{
    Task<PdfFile> BuildAsync(string docType, int docId);
}
