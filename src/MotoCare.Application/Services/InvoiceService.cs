using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;
using MotoCare.Domain.Constants;

namespace MotoCare.Application.Services;

/// <summary>
/// Invoicing rules:
///  - create / edit DRAFT invoices (job card items + additional items, discounts, tax)
///  - settle with one or more payment methods, including credit on the customer account
///  - void a settled invoice
/// </summary>
public sealed class InvoiceService(IInvoiceRepository repo, MasterService masters, IPermissionService permissions, ICurrentUser me,
                                   MessageService messages)   // Added: auto-send bill by e-mail / WhatsApp
{
    private static readonly string[] Sources = { "SERVICE", "PART", "EXTRA" };

    // =================================================================== load job card

    /// <summary>Header + every service and part of a completed job card, ready to bill.</summary>
    public async Task<ResultBag> LoadFromJobAsync(int jobCardId)
    {
        var bag = await repo.LoadFromJobAsync(jobCardId);
        var job = Check.Found(bag.Row("job"), "Job card");

        Check.That(job.Get<string>("Status") == JobCardStatus.Completed, "Only completed job cards can be billed.");
        Check.That(job.Get<int?>("BilledInvoiceId") is null, "This job card has already been invoiced.");
        return bag;
    }

    // =================================================================== save draft

    public async Task<int> SaveAsync(InvoiceSaveDto dto)
    {
        var limits = await permissions.GetLimitsAsync(me.RoleId);

        // ---- 1. an existing invoice must still be a draft
        if (dto.InvoiceId > 0)
        {
            var existing = Check.Found((await repo.GetAsync(dto.InvoiceId)).Row("invoice"), "Invoice");
            Check.That(existing.Get<string>("PayStatus") == InvoiceStatus.Draft, "Only draft invoices can be changed.");
        }

        // ---- 2. job card rules (the customer always comes from the job card)
        int? customerId = dto.CustomerId;
        if (dto.JobCardId is > 0)
        {
            var job = Check.Found((await repo.LoadFromJobAsync(dto.JobCardId.Value)).Row("job"), "Job card");
            Check.That(job.Get<string>("Status") == JobCardStatus.Completed, "Only completed job cards can be billed.");
            Check.That(job.Get<int?>("BilledInvoiceId") is null, "This job card has already been invoiced.");

            var draftId = job.Get<int?>("DraftInvoiceId");
            Check.That(draftId is null || draftId == dto.InvoiceId, "A draft invoice already exists for this job card.");
            customerId = job.Get<int>("CustomerId");
        }

        // ---- 3. items
        var items = await ValidateItemsAsync(dto, limits);

        // ---- 4. totals
        var discountType = dto.BillDiscountType == "A" ? "A" : "P";
        Check.NotNegative(dto.BillDiscountValue, "Bill discount");
        if (discountType == "P") Check.That(dto.BillDiscountValue <= 100, "Bill discount cannot be more than 100%.");

        decimal taxRate = 0;
        if (dto.TaxId is > 0)
        {
            var tax = Check.Found(await masters.GetAsync("tax-rates", dto.TaxId.Value), "Tax rate");
            taxRate = tax.Get<decimal>("RatePct");
        }

        var totals = InvoiceCalculator.Calculate(items, discountType, dto.BillDiscountValue, taxRate);
        Check.That(totals.BillDiscountAmt <= totals.SubTotal - totals.LineDiscount, "Bill discount is larger than the bill.");

        // ---- 5. discount limit of the user type
        if (!limits.IsAdmin)
            Check.That(totals.DiscountPct <= limits.MaxDiscountPct,
                $"Total discount of {totals.DiscountPct:0.#}% is above your limit of {limits.MaxDiscountPct:0.#}%.");

        var model = new InvoiceSaveModel(
            dto.InvoiceId, dto.InvoiceDate.Date, dto.JobCardId, customerId,
            totals.SubTotal, totals.LineDiscount, discountType, dto.BillDiscountValue, totals.BillDiscountAmt,
            dto.TaxId, totals.TaxAmt, totals.NetTotal, dto.Notes?.Trim(), me.UserId, items);

        return await repo.SaveAsync(model);
    }

    /// <summary>Checks every line and fills in descriptions from the catalogue.</summary>
    private async Task<List<InvoiceItemDto>> ValidateItemsAsync(InvoiceSaveDto dto, RoleLimits limits)
    {
        Check.That(dto.Items is { Count: > 0 }, "Add at least one item to the bill.");

        var parts = (await masters.ListAsync("parts")).ToDictionary(p => p.Get<int>("PartId"));
        var services = (await masters.ListAsync("services")).ToDictionary(s => s.Get<int>("ServiceId"));
        bool canEditPrice = limits.IsAdmin || limits.CanEditPrice;
        var result = new List<InvoiceItemDto>();

        foreach (var raw in dto.Items!)
        {
            var item = raw with
            {
                Source = (raw.Source ?? "").ToUpperInvariant(),
                FromJob = raw.FromJob && dto.JobCardId is > 0,     // job lines only exist on job card invoices
                Description = raw.Description?.Trim()
            };

            Check.That(Sources.Contains(item.Source), "Item type must be SERVICE, PART or EXTRA.");
            Check.Positive(item.Qty, "Quantity");
            Check.NotNegative(item.UnitPrice, "Unit price");
            Check.That(item.Discount >= 0 && item.Discount <= item.Qty * item.UnitPrice, "A line discount is larger than the line value.");

            if (item.Source == "PART")
            {
                Check.That(item.PartId.HasValue && parts.ContainsKey(item.PartId.Value), "Part lines must have a part selected.");
                var part = parts[item.PartId!.Value];
                item = item with { Description = string.IsNullOrEmpty(item.Description) ? part.Get<string>("Description") : item.Description };

                // additional parts: price must match the catalogue unless the user may edit prices
                if (!item.FromJob && !canEditPrice)
                    Check.That(item.UnitPrice == part.Get<decimal>("SellPrice"), "Your user type cannot change catalogue prices on an invoice.");
            }
            else if (item.Source == "SERVICE")
            {
                Check.That(item.ServiceId.HasValue && services.ContainsKey(item.ServiceId.Value), "Service lines must have a service selected.");
                var service = services[item.ServiceId!.Value];
                item = item with { Description = string.IsNullOrEmpty(item.Description) ? service.Get<string>("Name") : item.Description };

                if (!item.FromJob && !canEditPrice)
                    Check.That(item.UnitPrice == service.Get<decimal>("LabourCharge"), "Your user type cannot change catalogue prices on an invoice.");
            }
            else
            {
                Check.Required(item.Description, "Extra item description");
            }

            result.Add(item);
        }

        // stock check for additional parts (job card parts already left stock when issued)
        var extraPartQty = result.Where(i => i.Source == "PART" && !i.FromJob)
                                 .GroupBy(i => i.PartId!.Value)
                                 .Select(g => new { PartId = g.Key, Qty = g.Sum(i => i.Qty) });
        foreach (var x in extraPartQty)
        {
            var part = parts[x.PartId];
            var stock = part.Get<decimal>("StockQty");
            Check.That(stock >= x.Qty, $"Not enough stock for {part.Get<string>("PartNo")} (available {stock:0.##}).");
        }

        return result;
    }

    // =================================================================== delete draft

    public async Task DeleteDraftAsync(int invoiceId)
    {
        var invoice = Check.Found((await repo.GetAsync(invoiceId)).Row("invoice"), "Invoice");
        Check.That(invoice.Get<string>("PayStatus") == InvoiceStatus.Draft, "Only draft invoices can be deleted. Void a settled invoice instead.");
        await repo.DeleteDraftAsync(invoiceId);
    }

    // =================================================================== settle / receive payment

    /// <summary>
    /// First call on a draft finalises the bill. Later calls receive money against the open balance.
    /// Lines of a "credit" payment type are not money received: they leave the amount on the customer account.
    /// </summary>
    public async Task<RowData?> SettleAsync(int invoiceId, SettleDto dto)
    {
        var limits = await permissions.GetLimitsAsync(me.RoleId);
        var invoice = Check.Found((await repo.GetAsync(invoiceId)).Row("invoice"), "Invoice");
        var status = invoice.Get<string>("PayStatus");
        Check.That(status != InvoiceStatus.Void, "This invoice is void.");
        Check.That(status != InvoiceStatus.Paid, "This invoice is already fully paid.");

        bool isFirstSettlement = status == InvoiceStatus.Draft;
        decimal amountDue = isFirstSettlement ? invoice.Get<decimal>("NetTotal") : invoice.Get<decimal>("Balance");
        int? customerId = invoice.Get<int?>("CustomerId");
        var lines = dto.Lines ?? new List<PaymentLineDto>();

        // ---- 1. check each line against its payment type settings
        var types = (await masters.ListAsync("payment-types", activeOnly: true)).ToDictionary(t => t.Get<int>("PaymentTypeId"));
        var paidLines = new List<PaymentLineDto>();
        var creditLines = new List<PaymentLineDto>();

        foreach (var line in lines)
        {
            Check.That(types.TryGetValue(line.PaymentTypeId, out var type) && type.Get<bool>("AllowCustomer"),
                       "A payment type is not available for customer receipts.");
            ValidatePaymentLine(line, type!);

            if (type!.Get<bool>("IsCredit")) creditLines.Add(line);
            else paidLines.Add(line);
        }

        decimal paidNow = paidLines.Sum(l => l.Amount);
        decimal onCredit = creditLines.Sum(l => l.Amount);
        decimal change = paidLines.Where(l => l.Tendered > l.Amount).Sum(l => l.Tendered!.Value - l.Amount);
        decimal leftOnAccount = amountDue - paidNow;

        // ---- 2. amounts
        Check.That(paidNow + onCredit <= amountDue,
            $"Payments of {paidNow + onCredit:N2} are more than the amount due of {amountDue:N2}. Enter cash tendered to give change.");

        if (!isFirstSettlement)
        {
            Check.That(onCredit == 0, "Credit can only be given when a bill is first settled. The balance is already on the customer account.");
            Check.That(paidNow > 0, "Add at least one payment line.");
        }

        // ---- 3. anything not paid now goes to the customer account -> credit rules
        if (isFirstSettlement && leftOnAccount > 0)
        {
            Check.That(customerId is not null, "Walk-in sales must be paid in full.");
            Check.That(limits.IsAdmin || limits.CanGiveCredit, "Your user type cannot leave a balance on the customer account.");

            decimal creditLimit = invoice.Get<decimal>("CreditLimit");
            decimal outstanding = invoice.Get<decimal>("Outstanding");
            bool canOverride = dto.OverrideCredit && (limits.IsAdmin || limits.CanOverrideCredit);

            Check.That(outstanding + leftOnAccount <= creditLimit || canOverride,
                $"Credit limit exceeded. Limit {creditLimit:N2}, outstanding {outstanding:N2}, this bill leaves {leftOnAccount:N2}. " +
                "Take more payment or ask an admin to override.");
        }

        // ---- 4. due date of the credit part: from the credit line, else customer payment terms
        DateTime? creditDueDate = null;
        if (isFirstSettlement && leftOnAccount > 0)
        {
            creditDueDate = creditLines.Where(l => l.DueDate.HasValue).Select(l => l.DueDate).Max()
                            ?? DateTime.Today.AddDays(invoice.Get<int>("PaymentTermsDays"));
        }

        var model = new SettleModel(invoiceId, me.UserId, paidNow, change, creditDueDate, paidLines);
        var result = await repo.SettleAsync(model);

        // Added: when the bill is first settled, queue it to the customer (if auto-send is on in Messages settings)
        if (isFirstSettlement) await messages.AutoSendAsync("INVOICE", invoiceId);
        return result;
    }

    /// <summary>Checks the details a payment type requires (Master Files > Payment Types).</summary>
    private static void ValidatePaymentLine(PaymentLineDto line, RowData type)
    {
        string name = type.Get<string>("Name");
        Check.Positive(line.Amount, $"{name} amount");
        if (line.Tendered.HasValue)
            Check.That(line.Tendered.Value >= line.Amount, "Cash tendered is less than the cash amount.");

        var missing = new List<string>();
        if (type.Get<bool>("ReqCardType") && line.CardTypeId is null) missing.Add("card type");
        if (type.Get<bool>("ReqLast4") && (line.Last4?.Trim().Length ?? 0) != 4) missing.Add("last 4 digits");
        if (type.Get<bool>("ReqApproval") && string.IsNullOrWhiteSpace(line.ApprovalCode)) missing.Add("approval code");
        if (type.Get<bool>("ReqBank") && line.BankId is null) missing.Add("bank");
        if (type.Get<bool>("ReqRefNo") && string.IsNullOrWhiteSpace(line.ReferenceNo)) missing.Add("reference no");
        if (type.Get<bool>("ReqChequeDate") && line.ChequeDate is null) missing.Add("cheque date");
        if (type.Get<bool>("ReqDueDate") && line.DueDate is null) missing.Add("due date");

        Check.That(missing.Count == 0, $"{name} payment is missing: {string.Join(", ", missing)}.");
    }

    // =================================================================== void

    public async Task VoidAsync(int invoiceId, string? reason)
    {
        var limits = await permissions.GetLimitsAsync(me.RoleId);
        Check.That(limits.IsAdmin || limits.CanVoidInvoice, "Your user type cannot void invoices.");
        Check.Required(reason, "Reason");

        var invoice = Check.Found((await repo.GetAsync(invoiceId)).Row("invoice"), "Invoice");
        Check.That(invoice.Get<string>("PayStatus") != InvoiceStatus.Void, "This invoice is already void.");

        await repo.VoidAsync(invoiceId, reason!.Trim());
    }
}
