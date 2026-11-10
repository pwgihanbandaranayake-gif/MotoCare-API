using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>
/// Purchasing flow: Purchase order (Draft -> Approved) -> GRN (Pending -> Posted) -> Returns (debit notes).
/// </summary>
public sealed class PurchasingService(IPurchasingRepository repo, MasterService masters, ICurrentUser me)
{
    // =================================================================== purchase orders

    public async Task<int> SavePoAsync(PurchaseOrderSaveDto dto)
    {
        Check.Required(dto.SupplierId, "Supplier");
        var items = dto.Items ?? new List<PoItemDto>();
        Check.That(items.Count > 0, "Add at least one part to the purchase order.");
        Check.That(items.All(i => i.PartId > 0), "Every line needs a part.");
        Check.That(items.All(i => i.QtyOrdered > 0), "Every line needs a quantity greater than zero.");
        Check.That(items.All(i => i.UnitPrice >= 0), "Unit price cannot be negative.");
        Check.That(items.Select(i => i.PartId).Distinct().Count() == items.Count, "The same part is listed twice.");
        if (dto.ExpectedDate.HasValue)
            Check.That(dto.ExpectedDate.Value.Date >= dto.PODate.Date, "Expected date cannot be before the PO date.");

        if (dto.POId > 0)
        {
            var po = Check.Found((await repo.PoGetAsync(dto.POId)).Row("po"), "Purchase order");
            Check.That(po.Get<string>("Status") == "Draft", "Only draft purchase orders can be changed.");
        }

        return await repo.PoSaveAsync(dto, items, me.UserId);
    }

    /// <summary>APPROVE / REJECT a draft, or CLOSE an approved / received order.</summary>
    public async Task SetPoStatusAsync(int poId, string? action)
    {
        var bag = await repo.PoGetAsync(poId);
        var po = Check.Found(bag.Row("po"), "Purchase order");
        var status = po.Get<string>("Status");

        switch ((action ?? "").ToUpperInvariant())
        {
            case "APPROVE":
                Check.That(status == "Draft", "Only draft purchase orders can be approved.");
                Check.That(bag.Rows("items").Count > 0, "The purchase order has no items.");
                await repo.PoSetStatusAsync(poId, "Approved", me.UserId);
                break;

            case "REJECT":
                Check.That(status == "Draft", "Only draft purchase orders can be rejected.");
                await repo.PoSetStatusAsync(poId, "Rejected", me.UserId);
                break;

            case "CLOSE":
                Check.That(status is "Approved" or "Partly received" or "Received", "Only approved or received purchase orders can be closed.");
                var pendingGrns = await repo.GrnListAsync("Pending", null);
                Check.That(pendingGrns.All(g => g.Get<int>("POId") != poId), "Post or delete the pending GRN first.");
                await repo.PoSetStatusAsync(poId, "Closed", me.UserId);
                break;

            default:
                throw new Domain.BusinessRuleException("Unknown action. Use APPROVE, REJECT or CLOSE.");
        }
    }

    // =================================================================== GRN

    /// <summary>PO lines still to receive. The PO must be approved.</summary>
    public async Task<List<RowData>> PendingFromPoAsync(int poId)
    {
        var po = Check.Found((await repo.PoGetAsync(poId)).Row("po"), "Purchase order");
        Check.That(po.Get<string>("Status") is "Approved" or "Partly received", "Goods can only be received against an approved purchase order.");
        return await repo.GrnPendingFromPoAsync(poId);
    }

    public async Task<int> SaveGrnAsync(GrnSaveDto dto)
    {
        if (dto.GRNId > 0)
        {
            var grn = Check.Found((await repo.GrnGetAsync(dto.GRNId)).Row("grn"), "GRN");
            Check.That(grn.Get<string>("Status") == "Pending", "Posted GRNs cannot be changed. Use a return / debit note instead.");
        }

        // only lines with a received quantity are saved
        var lines = (dto.Items ?? new List<GrnItemDto>()).Where(i => i.QtyReceived > 0).ToList();
        Check.That(lines.Count > 0, "Enter the quantity received for at least one item.");
        foreach (var line in lines)
            Check.That(line.QtyRejected >= 0 && line.QtyRejected <= line.QtyReceived, "Rejected quantity cannot be more than received.");

        return dto.POId is > 0 ? await SaveGrnWithPoAsync(dto, lines) : await SaveGrnWithoutPoAsync(dto, lines);
    }

    /// <summary>GRN against an approved PO: every line must be a pending PO line.</summary>
    private async Task<int> SaveGrnWithPoAsync(GrnSaveDto dto, List<GrnItemDto> lines)
    {
        int poId = dto.POId!.Value;
        var po = Check.Found((await repo.PoGetAsync(poId)).Row("po"), "Purchase order");
        var pending = (await PendingFromPoAsync(poId)).ToDictionary(p => p.Get<int>("POItemId"));

        var items = new List<GrnItemModel>();
        foreach (var line in lines)
        {
            Check.That(line.POItemId.HasValue && pending.ContainsKey(line.POItemId.Value), "An item is not pending on this purchase order.");
            var poLine = pending[line.POItemId!.Value];

            decimal accepted = line.QtyReceived - line.QtyRejected;
            decimal qtyPending = poLine.Get<decimal>("QtyPending");
            Check.That(accepted <= qtyPending,
                $"Accepted quantity for {poLine.Get<string>("PartNo")} is more than the {qtyPending:0.##} still pending on the PO.");

            decimal cost = line.CostPrice ?? poLine.Get<decimal>("UnitPrice");
            Check.NotNegative(cost, "Cost price");

            items.Add(new GrnItemModel(line.POItemId, poLine.Get<int>("PartId"), line.QtyReceived, line.QtyRejected, cost,
                                       string.IsNullOrWhiteSpace(line.BatchNo) ? null : line.BatchNo.Trim(), line.ExpiryDate));
        }

        var model = new GrnSaveModel(dto.GRNId, poId, po.Get<int>("SupplierId"), dto.GRNDate.Date, me.UserId, items);
        return await repo.GrnSaveAsync(model);
    }

    /// <summary>Added: GRN without a PO (direct purchase). Supplier and parts are chosen on the GRN.</summary>
    private async Task<int> SaveGrnWithoutPoAsync(GrnSaveDto dto, List<GrnItemDto> lines)
    {
        Check.That(dto.SupplierId is > 0, "Select the supplier.");
        Check.Found(await masters.GetAsync("suppliers", dto.SupplierId!.Value), "Supplier");
        Check.That(lines.All(l => l.PartId is > 0), "Select the part on every line.");
        Check.That(lines.Select(l => l.PartId).Distinct().Count() == lines.Count, "The same part is listed twice.");

        var items = new List<GrnItemModel>();
        foreach (var line in lines)
        {
            var part = Check.Found(await masters.GetAsync("parts", line.PartId!.Value), "Part");
            decimal cost = line.CostPrice ?? part.Get<decimal>("CostPrice");
            Check.NotNegative(cost, "Cost price");
            items.Add(new GrnItemModel(null, line.PartId.Value, line.QtyReceived, line.QtyRejected, cost,
                                       string.IsNullOrWhiteSpace(line.BatchNo) ? null : line.BatchNo.Trim(), line.ExpiryDate));
        }

        var model = new GrnSaveModel(dto.GRNId, null, dto.SupplierId.Value, dto.GRNDate.Date, me.UserId, items);
        return await repo.GrnSaveAsync(model);
    }

    public async Task PostGrnAsync(int grnId)
    {
        var bag = await repo.GrnGetAsync(grnId);
        var grn = Check.Found(bag.Row("grn"), "GRN");
        Check.That(grn.Get<string>("Status") == "Pending", "Only pending GRNs can be posted.");

        // re-check: another GRN may have been posted for the same PO meanwhile (GRN without PO: nothing to check)
        int poId = grn.Get<int>("POId");
        var pending = poId > 0
            ? (await repo.GrnPendingFromPoAsync(poId)).ToDictionary(p => p.Get<int>("POItemId"))
            : new Dictionary<int, RowData>();
        foreach (var item in poId > 0 ? bag.Rows("items") : new List<RowData>())
        {
            decimal accepted = item.Get<decimal>("QtyAccepted");
            decimal qtyPending = pending.TryGetValue(item.Get<int>("POItemId"), out var p) ? p.Get<decimal>("QtyPending") : 0;
            Check.That(accepted <= qtyPending, $"Accepted quantity for {item.Get<string>("PartNo")} is now more than the pending PO quantity.");
        }

        await repo.GrnPostAsync(grnId);
    }

    public async Task DeleteGrnAsync(int grnId)
    {
        var grn = Check.Found((await repo.GrnGetAsync(grnId)).Row("grn"), "GRN");
        Check.That(grn.Get<string>("Status") == "Pending", "Only pending GRNs can be deleted.");
        await repo.GrnDeleteAsync(grnId);
    }

    // =================================================================== returns

    public async Task<int> SaveReturnAsync(int grnId, GrnReturnDto dto)
    {
        Check.Required(dto.Reason, "Reason");

        var bag = await repo.GrnGetAsync(grnId);
        var grn = Check.Found(bag.Row("grn"), "GRN");
        Check.That(grn.Get<string>("Status") == "Posted", "Returns can only be made against a posted GRN.");

        var grnItems = bag.Rows("items");
        var lines = (dto.Items ?? new List<GrnReturnItemDto>()).Where(i => i.Qty > 0).ToList();
        Check.That(lines.Count > 0, "Enter a quantity to return for at least one item.");

        var items = new List<ReturnItemModel>();
        foreach (var line in lines)
        {
            var grnItem = grnItems.FirstOrDefault(i => i.Get<int>("PartId") == line.PartId);
            Check.That(grnItem is not null, "A returned part is not on this GRN.");

            // can return at most: accepted - already returned
            decimal canReturn = grnItem!.Get<decimal>("QtyAccepted") - grnItem.Get<decimal>("QtyReturned");
            Check.That(line.Qty <= canReturn, $"Only {canReturn:0.##} of {grnItem.Get<string>("PartNo")} can be returned.");

            decimal cost = line.UnitCost ?? grnItem.Get<decimal>("CostPrice");
            items.Add(new ReturnItemModel(line.PartId, line.Qty, cost));
        }

        return await repo.ReturnSaveAsync(grnId, dto.ReturnDate.Date, dto.Reason!.Trim(), items);
    }
}
