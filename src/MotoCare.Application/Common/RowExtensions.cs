using System.Globalization;
using MotoCare.Application.Abstractions;

namespace MotoCare.Application.Common;

/// <summary>
/// Helpers to read typed values from stored-procedure rows (RowData) and result bags.
/// Example: int status = job.Get&lt;string&gt;("Status");
/// </summary>
public static class RowExtensions
{
    /// <summary>Reads a column and converts it to T. Returns <paramref name="fallback"/> when the column is null or missing.</summary>
    public static T Get<T>(this RowData row, string column, T fallback = default!)
    {
        if (!row.TryGetValue(column, out var value) || value is null) return fallback;
        if (value is T typed) return typed;

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }

    /// <summary>Single-row result set from a ResultBag (null when empty).</summary>
    public static RowData? Row(this ResultBag bag, string name) => bag.TryGetValue(name, out var v) ? v as RowData : null;

    /// <summary>Multi-row result set from a ResultBag (empty list when missing).</summary>
    public static List<RowData> Rows(this ResultBag bag, string name) =>
        bag.TryGetValue(name, out var v) && v is List<RowData> list ? list : new List<RowData>();
}
