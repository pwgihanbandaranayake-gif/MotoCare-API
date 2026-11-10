using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>Vehicle edits and service reminders.</summary>
public sealed class VehicleService(IVehicleRepository repo, ICustomerRepository customers)
{
    private static readonly string[] ReminderStatuses = { "Due", "SMS sent", "Completed", "Cancelled" };

    public async Task<int> SaveAsync(VehicleSaveDto dto)
    {
        Check.Required(dto.CustomerId, "Customer");
        Check.Required(dto.RegNo, "Number plate");
        Check.That(dto.Odometer >= 0, "Odometer cannot be negative.");

        var regNo = dto.RegNo!.Trim().ToUpperInvariant();
        var dupe = await customers.CheckDuplicateAsync(regNo, null, dto.CustomerId);
        var owner = dupe?.Get<string>("PlateOwner");
        Check.That(owner is null, $"Number plate {regNo} is already registered to {owner}.");

        return await repo.SaveAsync(dto with { RegNo = regNo });
    }

    public async Task<int> SaveReminderAsync(ReminderSaveDto dto)
    {
        Check.Required(dto.VehicleId, "Vehicle");
        Check.That(dto.DueDate.HasValue, "Due date is required.");
        Check.That(dto.DueKm is null or >= 0, "Due km cannot be negative.");
        return await repo.SaveReminderAsync(dto);
    }

    public async Task SetReminderStatusAsync(int reminderId, string? status)
    {
        Check.That(ReminderStatuses.Contains(status), "Invalid reminder status.");
        await repo.SetReminderStatusAsync(reminderId, status!);
    }
}
