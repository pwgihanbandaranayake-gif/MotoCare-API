using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;
using MotoCare.Domain.Constants;

namespace MotoCare.Application.Services;

/// <summary>Customer master: validation for new / edited customers and their vehicles.</summary>
public sealed class CustomerService(ICustomerRepository repo, ISettingsRepository settings, MasterService masters)
{
    public async Task<int> SaveAsync(CustomerSaveDto dto)
    {
        // ---- 1. required fields
        Check.Required(dto.CustomerName, "Customer name");
        Check.Required(dto.Mobile, "Mobile number");
        Check.Required(dto.CustomerTypeId, "Customer type");
        Check.NotNegative(dto.CreditLimit, "Credit limit");
        Check.That(dto.PaymentTermsDays >= 0, "Payment terms cannot be negative.");

        // ---- 2. identify by: use the default from Settings when not sent
        var identifyBy = dto.IdentifyBy;
        if (string.IsNullOrWhiteSpace(identifyBy))
        {
            var all = await settings.ListAsync();
            identifyBy = all.FirstOrDefault(s => s.Get<string>("SettingKey") == "CustomerIdentifyDefault")?.Get<string>("SettingValue")
                         ?? CustomerIdentifyBy.Plate;
        }
        Check.That(identifyBy is CustomerIdentifyBy.Plate or CustomerIdentifyBy.Name, "Identify by must be PLATE or NAME.");

        // ---- 3. vehicles: clean plates, ignore empty rows
        var vehicles = (dto.Vehicles ?? new List<VehicleLineDto>())
            .Where(v => !string.IsNullOrWhiteSpace(v.RegNo))
            .Select(v => v with { RegNo = v.RegNo!.Trim().ToUpperInvariant(), Odometer = v.Odometer ?? 0 })
            .ToList();

        Check.That(vehicles.Select(v => v.RegNo).Distinct().Count() == vehicles.Count, "The same number plate is entered twice.");
        Check.That(vehicles.All(v => v.Odometer >= 0), "Odometer cannot be negative.");
        if (dto.CustomerId == 0 && identifyBy == CustomerIdentifyBy.Plate)
            Check.That(vehicles.Count > 0, "Add at least one vehicle when the customer is identified by number plate.");

        // ---- 4. duplicates: mobile and plates must not belong to another customer
        var mobile = dto.Mobile!.Trim();
        var mobileCheck = await repo.CheckDuplicateAsync(null, mobile, dto.CustomerId);
        var mobileOwner = mobileCheck?.Get<string>("MobileOwner");
        Check.That(mobileOwner is null, $"Mobile {mobile} is already registered to {mobileOwner}.");

        foreach (var v in vehicles)
        {
            var plateCheck = await repo.CheckDuplicateAsync(v.RegNo, null, dto.CustomerId);
            var plateOwner = plateCheck?.Get<string>("PlateOwner");
            Check.That(plateOwner is null, $"Number plate {v.RegNo} is already registered to {plateOwner}.");
        }

        // ---- Added: how bills are sent (e-mail needs an address)
        var sendVia = string.IsNullOrWhiteSpace(dto.SendBillVia) ? "None" : dto.SendBillVia.Trim();
        Check.That(sendVia is "None" or "Email" or "WhatsApp" or "Both", "Send bills via must be None, Email, WhatsApp or Both.");
        Check.That(sendVia is not ("Email" or "Both") || !string.IsNullOrWhiteSpace(dto.Email), "Enter the e-mail address to send bills by e-mail.");

        // ---- 5. cash customers have no credit limit
        var type = Check.Found(await masters.GetAsync("customer-types", dto.CustomerTypeId), "Customer type");
        var creditLimit = type.Get<bool>("IsCredit") ? dto.CreditLimit : 0;

        var clean = dto with
        {
            IdentifyBy = identifyBy,
            CustomerName = dto.CustomerName!.Trim(),
            Mobile = mobile,
            CreditLimit = creditLimit,
            SendBillVia = sendVia
        };
        return await repo.SaveAsync(clean, vehicles);
    }
}
