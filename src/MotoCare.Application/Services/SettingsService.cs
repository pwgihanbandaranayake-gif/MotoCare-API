using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;
using MotoCare.Domain.Constants;

namespace MotoCare.Application.Services;

/// <summary>Company settings and document numbering.</summary>
public sealed class SettingsService(ISettingsRepository repo)
{
    public async Task SaveAsync(string key, string? value)
    {
        Check.Required(key, "Setting");

        // phone and address may be blank; every other setting needs a value
        bool optional = key is "CompanyPhone" or "CompanyAddress" or "CompanyEmail" or "QuotationTerms";   // Fixed: e-mail / terms may be blank too
        if (!optional) Check.Required(value, "Value");

        // the default customer identification can only be PLATE or NAME
        if (key == "CustomerIdentifyDefault")
            Check.That(value is CustomerIdentifyBy.Plate or CustomerIdentifyBy.Name, "Choose number plate or customer name.");

        await repo.SaveAsync(key, (value ?? "").Trim());
    }

    public async Task SaveDocNumberAsync(DocNumberDto dto)
    {
        Check.Required(dto.Prefix, "Prefix");
        Check.That(dto.NextNo >= 1, "Next number must be 1 or more.");
        Check.That(dto.Padding is >= 1 and <= 10, "Digits must be between 1 and 10.");
        await repo.SaveDocNumberAsync(dto);
    }
}
