namespace MotoCare.Application.Dtos;

// =====================================================================
// Added: requests and models for quotations, stock counting, payroll and messages.
// Records used for table-valued parameters keep the SAME property order as the SQL type.
// =====================================================================

// ---------- quotations ----------
public sealed record QuotationSaveDto(int QuotationId, DateTime QuoteDate, DateTime? ValidUntil, int? CustomerId, string? CustomerName,
                                      string? Mobile, string? Email, int? VehicleId, string? VehicleText, string? BillDiscountType,
                                      decimal BillDiscountValue, int? TaxId, string? Notes, List<InvoiceItemDto>? Items);
public sealed record QuotationStatusDto(string? Status, int? InvoiceId);

// ---------- stock counting ----------
public sealed record StockCountCreateDto(DateTime CountDate, int? CategoryId, int? MakeId, string? Notes);
public sealed record StockCountLineDto(int PartId, decimal? CountedQty);          // dbo.StockCountItemTvp

// ---------- payroll ----------
public sealed record ServiceCommissionDto(int ServiceId, decimal? CommissionPct);    // dbo.ServiceCommissionTvp
public sealed record EmployeeAllowanceDto(int AllowanceTypeId, decimal? Amount);     // dbo.EmployeeAllowanceTvp
public sealed record PayrollProfileDto(decimal BasicSalary, decimal DefaultCommissionPct, bool IsOnPayroll,
                                       List<ServiceCommissionDto>? Commissions, List<EmployeeAllowanceDto>? Allowances);
public sealed record PayrollRunDto(DateTime FromDate, DateTime ToDate, bool PayFullBasic, string? Notes);
/// <summary>Added: money an employee took (Advance) or spent from the cash box (Expense); deducted in the next payroll run.</summary>
public sealed record AdvanceSaveDto(int AdvanceId, int EmployeeId, DateTime AdvanceDate, string? AdvanceType, string? Description, decimal Amount);

public sealed record PayrollExtraDto(int? EmployeeId, string? Description, decimal Amount, bool IsDeduction, bool EpfApplicable);

/// <summary>One calculated salary line (dbo.PayrollLineTvp).</summary>
public sealed record PayrollLineModel(int EmployeeId, decimal Basic, decimal Commission, decimal FixedAllowances, decimal ExtraAllowances,
                                      decimal Gross, decimal EpfBase, decimal EpfEmployee, decimal EpfEmployer, decimal Etf,
                                      decimal Deductions, decimal NetPay);

/// <summary>Payslip breakdown row (dbo.PayrollDetailTvp). DetailType = COMMISSION or ALLOWANCE.</summary>
public sealed record PayrollDetailModel(int EmployeeId, string DetailType, string Description, decimal BaseAmount, decimal Pct, decimal Amount);

public sealed record PayrollRunModel(int RunId, DateTime FromDate, DateTime ToDate, bool PayFullBasic, bool EpfEnabled,
                                     decimal EpfEmployeePct, decimal EpfEmployerPct, decimal EtfPct, string? Notes, int UserId,
                                     List<PayrollLineModel> Lines, List<PayrollDetailModel> Details);

// ---------- messages (e-mail / WhatsApp) ----------
/// <summary>Send a document now. DocType: INVOICE, PAYSLIP or QUOTATION. Empty EmailTo / MobileTo = use the contact's.</summary>
public sealed record SendDocumentDto(string? DocType, int DocId, bool Email, bool WhatsApp, string? EmailTo, string? MobileTo);

/// <summary>A generated PDF ready to attach.</summary>
public sealed record PdfFile(string FileName, byte[] Content, string Title);
