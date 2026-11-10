namespace MotoCare.Application.Dtos;

// =====================================================================
// Validated data passed from the services to the repositories.
// =====================================================================

/// <summary>User row used by login (mapped from sp_User_GetForLogin).</summary>
public sealed class LoginUserRecord
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public string? EmployeeName { get; set; }
    public decimal MaxDiscountPct { get; set; }
    public bool CanOverrideCredit { get; set; }
    public bool CanVoidInvoice { get; set; }
    public bool CanEditPrice { get; set; }
    public bool CanGiveCredit { get; set; }
}

/// <summary>Limits of a user type (Roles table). Admin is never limited.</summary>
public sealed record RoleLimits(bool IsAdmin, decimal MaxDiscountPct, bool CanOverrideCredit, bool CanVoidInvoice,
                                bool CanEditPrice, bool CanGiveCredit);

/// <summary>Invoice ready to save: items + totals calculated by InvoiceCalculator.</summary>
public sealed record InvoiceSaveModel(
    int InvoiceId, DateTime InvoiceDate, int? JobCardId, int? CustomerId,
    decimal SubTotal, decimal LineDiscount, string BillDiscountType, decimal BillDiscountValue, decimal BillDiscountAmt,
    int? TaxId, decimal TaxAmt, decimal NetTotal, string? Notes, int UserId,
    List<InvoiceItemDto> Items);

/// <summary>Settlement ready to post: only the money received now (non-credit lines).</summary>
public sealed record SettleModel(int InvoiceId, int UserId, decimal PaidNow, decimal ChangeGiven, DateTime? CreditDueDate,
                                 List<PaymentLineDto> PaidLines);

/// <summary>One GRN line with the part id filled in from the PO line.</summary>
public sealed record GrnItemModel(int? POItemId, int PartId, decimal QtyReceived, decimal QtyRejected, decimal CostPrice,
                                  string? BatchNo, DateTime? ExpiryDate);

public sealed record GrnSaveModel(int GRNId, int? POId, int SupplierId, DateTime GRNDate, int UserId, List<GrnItemModel> Items);

public sealed record ReturnItemModel(int PartId, decimal Qty, decimal UnitCost);
