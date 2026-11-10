using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>Supplier bills (what we owe) and supplier payments.</summary>
public sealed class SupplierBillService(ISupplierBillRepository repo, MasterService masters, ICurrentUser me)
{
    public async Task<int> SaveBillAsync(SupplierBillSaveDto dto)
    {
        Check.Required(dto.SupplierId, "Supplier");
        Check.Required(dto.BillNo, "Supplier bill number");
        Check.Positive(dto.Amount, "Bill amount");

        // a GRN can be billed once, and only to its own supplier
        if (dto.GRNId is > 0)
        {
            var unbilled = await repo.UnbilledGrnAsync(dto.SupplierId);
            Check.That(unbilled.Any(g => g.Get<int>("GRNId") == dto.GRNId), "This GRN is already billed or belongs to another supplier.");
        }

        // due date defaults to bill date + supplier payment terms
        var supplier = Check.Found(await masters.GetAsync("suppliers", dto.SupplierId), "Supplier");
        var dueDate = dto.DueDate?.Date ?? dto.BillDate.Date.AddDays(supplier.Get<int>("PaymentTermsDays"));
        Check.That(dueDate >= dto.BillDate.Date, "Due date cannot be before the bill date.");

        return await repo.SaveAsync(dto.SupplierId, dto.GRNId, dto.BillNo!.Trim(), dto.BillDate.Date, dueDate, dto.Amount);
    }

    public async Task<int> PayAsync(SupplierPaymentDto dto)
    {
        var bill = Check.Found(await repo.GetAsync(dto.BillId), "Bill");
        decimal balance = bill.Get<decimal>("Balance");

        Check.Positive(dto.Amount, "Payment amount");
        Check.That(dto.Amount <= balance, $"Payment is more than the bill balance of {balance:N2}.");

        var type = Check.Found(await masters.GetAsync("payment-types", dto.PaymentTypeId), "Payment type");
        Check.That(type.Get<bool>("IsActive") && type.Get<bool>("AllowSupplier"), "This payment type cannot be used for supplier payments.");
        if (type.Get<bool>("ReqBank")) Check.Required(dto.BankId, "Bank");
        if (type.Get<bool>("ReqRefNo")) Check.Required(dto.ReferenceNo, "Reference / cheque number");

        return await repo.PayAsync(dto, me.UserId);
    }
}
