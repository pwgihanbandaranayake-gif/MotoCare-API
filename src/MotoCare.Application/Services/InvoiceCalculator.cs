using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>Result of an invoice calculation. All amounts are rounded to 2 decimals.</summary>
public sealed record InvoiceTotals(decimal SubTotal, decimal LineDiscount, decimal BillDiscountAmt, decimal TaxAmt, decimal NetTotal)
{
    /// <summary>Total discount as a percentage of the sub total (used for the user type's discount limit).</summary>
    public decimal DiscountPct => SubTotal == 0 ? 0 : Math.Round((LineDiscount + BillDiscountAmt) * 100 / SubTotal, 2);
}

/// <summary>
/// Invoice maths in one place (the React screen uses the same formula):
///   sub total     = sum(qty x price)
///   after lines   = sub total - line discounts
///   bill discount = % of "after lines" or a fixed amount
///   tax           = (after lines - bill discount) x tax rate
///   net total     = after lines - bill discount + tax
/// </summary>
public static class InvoiceCalculator
{
    public static InvoiceTotals Calculate(IEnumerable<InvoiceItemDto> items, string billDiscountType, decimal billDiscountValue, decimal taxRatePct)
    {
        var list = items.ToList();
        decimal subTotal = Round(list.Sum(i => i.Qty * i.UnitPrice));
        decimal lineDiscount = Round(list.Sum(i => i.Discount));
        decimal afterLines = subTotal - lineDiscount;

        decimal billDiscount = billDiscountType == "P"
            ? Round(afterLines * billDiscountValue / 100)
            : Round(billDiscountValue);

        decimal tax = Round((afterLines - billDiscount) * taxRatePct / 100);
        decimal net = afterLines - billDiscount + tax;

        return new InvoiceTotals(subTotal, lineDiscount, billDiscount, tax, net);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
