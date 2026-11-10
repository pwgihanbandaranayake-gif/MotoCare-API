using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>
/// Added: quotations (estimates). Same item types and maths as invoices, but no stock or ledger effect.
/// Status flow: Open -> Sent -> Accepted / Rejected -> Converted (when an invoice is made from it).
/// </summary>
public sealed class QuotationService(IQuotationRepository repo, MasterService masters, ICurrentUser me)
{
    public static readonly string[] Statuses = { "Open", "Sent", "Accepted", "Rejected", "Converted" };

    public async Task<int> SaveAsync(QuotationSaveDto dto)
    {
        // ---- 1. existing quotation must still be editable
        if (dto.QuotationId > 0)
        {
            var current = Check.Found((await repo.GetAsync(dto.QuotationId)).Row("quotation"), "Quotation");
            Check.That(current.Get<string>("Status") != "Converted", "A converted quotation cannot be changed.");
        }

        // ---- 2. header
        Check.Required(dto.CustomerName, "Customer name");
        Check.That(dto.ValidUntil is null || dto.ValidUntil.Value.Date >= dto.QuoteDate.Date, "'Valid until' cannot be before the quotation date.");
        string discountType = dto.BillDiscountType == "A" ? "A" : "P";
        Check.NotNegative(dto.BillDiscountValue, "Bill discount");
        Check.That(discountType == "A" || dto.BillDiscountValue <= 100, "Bill discount % cannot be more than 100.");

        // ---- 3. items (same rules as invoice items)
        var items = new List<InvoiceItemDto>();
        foreach (var i in dto.Items ?? new List<InvoiceItemDto>())
        {
            string source = (i.Source ?? "").ToUpperInvariant();
            Check.That(source is "SERVICE" or "PART" or "EXTRA", "Item type must be SERVICE, PART or EXTRA.");
            Check.Positive(i.Qty, "Quantity");
            Check.NotNegative(i.UnitPrice, "Price");
            Check.That(i.Discount >= 0 && i.Discount <= i.Qty * i.UnitPrice, "A line discount is larger than the line value.");

            string? description = i.Description?.Trim();
            if (source == "PART")
            {
                Check.That(i.PartId is > 0, "Select the part on every part line.");
                var part = Check.Found(await masters.GetAsync("parts", i.PartId!.Value), "Part");
                description = string.IsNullOrWhiteSpace(description) ? part.Get<string>("Description") : description;
            }
            else if (source == "SERVICE")
            {
                Check.That(i.ServiceId is > 0, "Select the service on every service line.");
                var service = Check.Found(await masters.GetAsync("services", i.ServiceId!.Value), "Service");
                description = string.IsNullOrWhiteSpace(description) ? service.Get<string>("Name") : description;
            }
            Check.Required(description, "Item description");

            items.Add(i with
            {
                Source = source, FromJob = false, Description = description,
                PartId = source == "PART" ? i.PartId : null, ServiceId = source == "SERVICE" ? i.ServiceId : null
            });
        }
        Check.That(items.Count > 0, "Add at least one item to the quotation.");

        // ---- 4. totals (InvoiceCalculator = same maths as invoices)
        decimal taxRate = 0;
        if (dto.TaxId is > 0)
            taxRate = Check.Found(await masters.GetAsync("tax-rates", dto.TaxId.Value), "Tax").Get<decimal>("RatePct");
        var t = InvoiceCalculator.Calculate(items, discountType, dto.BillDiscountValue, taxRate);
        Check.That(t.BillDiscountAmt <= t.SubTotal - t.LineDiscount, "Bill discount is larger than the quotation.");

        var totals = new InvoiceTotalsModel(t.SubTotal, t.LineDiscount, discountType, dto.BillDiscountValue, t.BillDiscountAmt, t.TaxAmt, t.NetTotal);
        var clean = dto with
        {
            CustomerName = dto.CustomerName!.Trim(),
            Mobile = Blank(dto.Mobile), Email = Blank(dto.Email), VehicleText = Blank(dto.VehicleText), Notes = Blank(dto.Notes),
            CustomerId = dto.CustomerId is > 0 ? dto.CustomerId : null, VehicleId = dto.VehicleId is > 0 ? dto.VehicleId : null,
            TaxId = dto.TaxId is > 0 ? dto.TaxId : null
        };
        return await repo.SaveAsync(clean, totals, items, me.UserId);
    }

    public async Task SetStatusAsync(int id, QuotationStatusDto dto)
    {
        var q = Check.Found((await repo.GetAsync(id)).Row("quotation"), "Quotation");
        string status = dto.Status?.Trim() ?? "";
        Check.That(Statuses.Contains(status), "Status must be Open, Sent, Accepted, Rejected or Converted.");
        Check.That(q.Get<string>("Status") != "Converted", "This quotation is already converted to an invoice.");
        Check.That(status != "Converted" || dto.InvoiceId is > 0, "Give the invoice the quotation was converted to.");
        await repo.SetStatusAsync(id, status, dto.InvoiceId);
    }

    public async Task DeleteAsync(int id)
    {
        var q = Check.Found((await repo.GetAsync(id)).Row("quotation"), "Quotation");
        Check.That(q.Get<string>("Status") != "Converted", "A converted quotation cannot be deleted.");
        await repo.DeleteAsync(id);
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
