using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>Manual customer ledger adjustments.</summary>
public sealed class LedgerService(ILedgerRepository repo)
{
    public async Task AdjustCustomerAsync(int customerId, LedgerAdjustDto dto)
    {
        Check.Required(customerId, "Customer");
        Check.NotNegative(dto.Debit, "Debit");
        Check.NotNegative(dto.Credit, "Credit");

        // exactly one side must have a value
        Check.That((dto.Debit > 0) != (dto.Credit > 0), "Enter either a debit or a credit amount.");
        Check.Required(dto.Notes, "Reason");

        await repo.AdjustCustomerAsync(customerId, dto.Debit, dto.Credit, dto.Notes!.Trim());
    }

    /// <summary>Default period is the last month; "from" must not be after "to".</summary>
    public static (DateTime From, DateTime To) Period(DateTime? from, DateTime? to)
    {
        var end = (to ?? DateTime.Today).Date;
        var start = (from ?? end.AddMonths(-1)).Date;
        Check.That(start <= end, "'From' date must be before 'To' date.");
        return (start, end);
    }
}

/// <summary>Manual stock adjustments (stock count differences, damages ...).</summary>
public sealed class StockService(IStockRepository repo, MasterService masters)
{
    public async Task AdjustAsync(StockAdjustDto dto)
    {
        Check.Required(dto.PartId, "Part");
        Check.That(dto.Qty != 0, "Enter a quantity to add (positive) or remove (negative).");
        Check.Required(dto.Notes, "Reason");

        var part = Check.Found(await masters.GetAsync("parts", dto.PartId), "Part");
        var stock = part.Get<decimal>("StockQty");
        Check.That(stock + dto.Qty >= 0, $"Only {stock:0.##} in stock. You cannot remove {-dto.Qty:0.##}.");

        await repo.AdjustAsync(dto.PartId, dto.Qty, dto.Notes!.Trim());
    }
}
