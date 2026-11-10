namespace MotoCare.Application.Abstractions;

/// <summary>One row returned by a stored procedure (column name -> value). Serialised to camelCase JSON.</summary>
public sealed class RowData : Dictionary<string, object?>
{
    public RowData() : base(StringComparer.OrdinalIgnoreCase) { }
}

/// <summary>Several named result sets returned by one stored procedure.</summary>
public sealed class ResultBag : Dictionary<string, object?> { }

public sealed record SavedId(int Id);
