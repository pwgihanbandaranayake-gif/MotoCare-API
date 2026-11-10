using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>
/// Added: stock counting.
///   1. Start a count  -> the system quantity of every part is copied to the count sheet
///   2. Enter counted quantities (can be saved many times)
///   3. Post           -> stock is adjusted by (counted - system) and written to the stock ledger as COUNT
/// Do not issue / receive stock between steps 1 and 3, or the difference will include those movements.
/// </summary>
public sealed class StockCountService(IStockCountRepository repo, ICurrentUser me)
{
    public async Task<int> CreateAsync(StockCountCreateDto dto)
    {
        Check.That(dto.CountDate.Date <= DateTime.Today, "Count date cannot be in the future.");
        return await repo.CreateAsync(dto with { Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim() }, me.UserId);
    }

    public async Task SaveCountsAsync(int countId, List<StockCountLineDto>? lines)
    {
        var bag = await repo.GetAsync(countId);
        var count = Check.Found(bag.Row("count"), "Stock count");
        Check.That(count.Get<string>("Status") == "Draft", "This stock count is already posted.");

        var partIds = bag.Rows("items").Select(r => r.Get<int>("PartId")).ToHashSet();
        var list = lines ?? new List<StockCountLineDto>();
        foreach (var l in list)
        {
            Check.That(partIds.Contains(l.PartId), "A part is not on this count sheet.");
            Check.That(l.CountedQty is null or >= 0, "Counted quantity cannot be negative.");
        }
        await repo.SaveCountsAsync(countId, list);
    }

    public async Task PostAsync(int countId)
    {
        var bag = await repo.GetAsync(countId);
        var count = Check.Found(bag.Row("count"), "Stock count");
        Check.That(count.Get<string>("Status") == "Draft", "This stock count is already posted.");
        Check.That(bag.Rows("items").Any(r => r.Get<decimal?>("CountedQty") is not null), "Enter the counted quantity for at least one part.");
        await repo.PostAsync(countId, me.UserId);
    }

    public async Task DeleteAsync(int countId)
    {
        var count = Check.Found((await repo.GetAsync(countId)).Row("count"), "Stock count");
        Check.That(count.Get<string>("Status") == "Draft", "A posted stock count cannot be deleted.");
        await repo.DeleteAsync(countId);
    }
}
