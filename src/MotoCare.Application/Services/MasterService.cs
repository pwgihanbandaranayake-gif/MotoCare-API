using System.Globalization;
using System.Text.Json;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Masters;

namespace MotoCare.Application.Services;

/// <summary>
/// Generic list / get / save / delete for every simple master file defined in MasterRegistry
/// (payment types, banks, vehicle makes, services, parts, suppliers ...).
/// Also used by other services to look up master rows (e.g. a part's price and stock).
/// </summary>
public sealed class MasterService(IMasterRepository repo)
{
    /// <summary>Finds the master definition from the URL name, e.g. "payment-types".</summary>
    public static MasterDefinition Resolve(string entity) =>
        MasterRegistry.All.TryGetValue(entity, out var def) ? def : throw new KeyNotFoundException($"Unknown master '{entity}'.");

    public Task<List<RowData>> ListAsync(string entity, string? search = null, bool activeOnly = false, int? parentId = null, string? filter = null) =>
        repo.ListAsync(Resolve(entity), Clean(search), activeOnly, parentId, Clean(filter));

    public Task<RowData?> GetAsync(string entity, int id) => repo.GetAsync(Resolve(entity), id);

    public Task<string> DeleteAsync(string entity, int id) => repo.DeleteAsync(Resolve(entity), id);

    /// <summary>Validates the JSON body against the master definition and saves it.</summary>
    public async Task<int> SaveAsync(string entity, JsonElement body)
    {
        var def = Resolve(entity);
        Check.That(body.ValueKind == JsonValueKind.Object, "Request body must be a JSON object.");

        // read properties case-insensitively (React sends camelCase)
        var props = body.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);

        int id = props.TryGetValue("id", out var idEl) ? (int?)ToValue(idEl, FieldKind.Int, "Id") ?? 0 : 0;
        bool isActive = !props.TryGetValue("isActive", out var activeEl) || ((bool?)ToValue(activeEl, FieldKind.Bool, "Active") ?? true);

        var values = new Dictionary<string, object?>();
        foreach (var field in def.Fields)
        {
            object? value = props.TryGetValue(field.Name, out var el) ? ToValue(el, field.Kind, field.Label) : null;

            if (field.Required)
                Check.That(value is not null && !(value is string s && s.Length == 0), $"{field.Label} is required.");

            // NOT NULL numeric / flag columns: blank means 0 / false
            if (value is null && field.ZeroWhenEmpty)
                value = field.Kind == FieldKind.Bool ? false : 0;

            if (value is decimal d) Check.NotNegative(d, field.Label);
            if (value is int i) Check.That(i >= 0, $"{field.Label} cannot be negative.");

            values[field.Name] = value;
        }

        return await repo.SaveAsync(def, id, values, isActive);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Converts a JSON value to the C# type of the column. Empty text becomes null.</summary>
    private static object? ToValue(JsonElement el, FieldKind kind, string label)
    {
        if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (el.ValueKind == JsonValueKind.True) return true;
        if (el.ValueKind == JsonValueKind.False) return false;

        var text = el.ValueKind == JsonValueKind.String ? el.GetString()?.Trim() : el.GetRawText();
        if (string.IsNullOrEmpty(text)) return null;

        try
        {
            return kind switch
            {
                FieldKind.Int => int.Parse(text, CultureInfo.InvariantCulture),
                FieldKind.Decimal => decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture),
                FieldKind.Bool => text == "1" || bool.Parse(text),
                FieldKind.Date => DateTime.Parse(text, CultureInfo.InvariantCulture),
                _ => text
            };
        }
        catch (FormatException)
        {
            throw new Domain.BusinessRuleException($"{label}: '{text}' is not a valid value.");
        }
    }
}
