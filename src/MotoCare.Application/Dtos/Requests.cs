namespace MotoCare.Application.Dtos;

// =====================================================================
// Request bodies sent by the React app (JSON, camelCase is bound automatically).
// =====================================================================

// ---------- auth / users ----------
public sealed record LoginRequest(string Username, string Password);
public sealed record UserSaveDto(int UserId, string Username, string? Password, int? EmployeeId, int RoleId, bool IsActive);
public sealed record PermissionDto(string ModuleKey, bool CanView, bool CanAdd, bool CanEdit, bool CanDelete, bool CanApprove);

// ---------- settings ----------
public sealed record SettingSaveDto(string Value);
public sealed record DocNumberDto(string DocType, string Prefix, int NextNo, int Padding);

// ---------- customers / vehicles ----------
public sealed record VehicleLineDto(int VehicleId, string? RegNo, int? ModelId, int? Year, string? ChassisNo, string? EngineNo,
                                    string? Colour, int? Odometer, int? ReminderIntervalKm);

public sealed record CustomerSaveDto(int CustomerId, string? IdentifyBy, string? CustomerName, string? Mobile, string? Nic, string? Email,
                                     string? Address, string? City, int CustomerTypeId, decimal CreditLimit, int PaymentTermsDays,
                                     bool IsActive, List<VehicleLineDto>? Vehicles,
                                     string? SendBillVia = null);   // Added: None / Email / WhatsApp / Both

public sealed record VehicleSaveDto(int VehicleId, int CustomerId, string? RegNo, int? ModelId, int? Year, string? ChassisNo, string? EngineNo,
                                    string? Colour, int Odometer, int? ReminderIntervalKm, bool IsActive);

public sealed record ReminderSaveDto(int ReminderId, int VehicleId, DateTime? DueDate, int? DueKm, string? Notes);
public sealed record StatusDto(string Status);

// ---------- appointments / job cards ----------
public sealed record AppointmentSaveDto(int AppointmentId, int CustomerId, int VehicleId, DateTime? ApptDate, string? ApptTime,
                                        string? ServiceRequired, string? Notes, string? Status);
public sealed record CheckInDto(int? EmployeeId, int Odometer, string? Complaints);

public sealed record JobCardSaveDto(int JobCardId, int CustomerId, int VehicleId, int? EmployeeId, int Odometer, string? Complaints, int? AppointmentId,
                                    int? NextServiceKm = null);   // Added: next service odometer
public sealed record JobServiceAddDto(int ServiceId, decimal? Charge);
public sealed record JobPartIssueDto(int PartId, decimal Qty, decimal? UnitPrice);

// ---------- invoices / settlement ----------
/// <summary>Source: SERVICE, PART or EXTRA. FromJob = line came from the job card.</summary>
public sealed record InvoiceItemDto(string Source, bool FromJob, int? ServiceId, int? PartId, string? Description,
                                    decimal Qty, decimal UnitPrice, decimal Discount);

/// <summary>BillDiscountType: "P" = percent, "A" = amount.</summary>
public sealed record InvoiceSaveDto(int InvoiceId, DateTime InvoiceDate, int? JobCardId, int? CustomerId, string? BillDiscountType,
                                    decimal BillDiscountValue, int? TaxId, string? Notes, List<InvoiceItemDto>? Items);

/// <summary>One payment line. Which detail fields are needed depends on the payment type (Master Files > Payment Types).</summary>
public sealed record PaymentLineDto(int PaymentTypeId, decimal Amount, decimal? Tendered, int? CardTypeId, string? Last4,
                                    string? ApprovalCode, int? BankId, string? ReferenceNo, DateTime? ChequeDate, DateTime? DueDate);

public sealed record SettleDto(List<PaymentLineDto>? Lines, bool OverrideCredit);
public sealed record VoidDto(string? Reason);

// ---------- ledgers / stock ----------
public sealed record LedgerAdjustDto(decimal Debit, decimal Credit, string? Notes);
public sealed record StockAdjustDto(int PartId, decimal Qty, string? Notes);

// ---------- purchasing ----------
public sealed record PoItemDto(int PartId, decimal QtyOrdered, decimal UnitPrice);
public sealed record PurchaseOrderSaveDto(int POId, int SupplierId, DateTime PODate, DateTime? ExpectedDate, List<PoItemDto>? Items);
public sealed record PoActionDto(string Action);

// GRN against a PO: send POItemId. GRN without PO (Added): leave POId null, send SupplierId and PartId per line.
public sealed record GrnItemDto(int? POItemId, int? PartId, decimal QtyReceived, decimal QtyRejected, decimal? CostPrice, string? BatchNo, DateTime? ExpiryDate);
public sealed record GrnSaveDto(int GRNId, int? POId, int? SupplierId, DateTime GRNDate, List<GrnItemDto>? Items);
public sealed record GrnReturnItemDto(int PartId, decimal Qty, decimal? UnitCost);
public sealed record GrnReturnDto(DateTime ReturnDate, string? Reason, List<GrnReturnItemDto>? Items);

public sealed record SupplierBillSaveDto(int SupplierId, int? GRNId, string? BillNo, DateTime BillDate, DateTime? DueDate, decimal Amount);
public sealed record SupplierPaymentDto(int BillId, DateTime PayDate, int PaymentTypeId, decimal Amount, int? BankId, string? ReferenceNo);
