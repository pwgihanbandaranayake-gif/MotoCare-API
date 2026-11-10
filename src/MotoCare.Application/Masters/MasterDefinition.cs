namespace MotoCare.Application.Masters;

public enum FieldKind { String, Int, Decimal, Bool, Date }

/// <summary>
/// One editable column of a master table.
/// ZeroWhenEmpty = the column is NOT NULL, so a blank number / flag is saved as 0 / false.
/// </summary>
public sealed record MasterField(string Name, string Label, FieldKind Kind, bool Required, bool ZeroWhenEmpty);

/// <summary>A simple master table served by the generated sp_{Table}_List / Get / Save / Delete procedures.</summary>
public sealed record MasterDefinition(string Table, string KeyColumn, string Module, IReadOnlyList<MasterField> Fields)
{
    public string ListProc => $"dbo.sp_{Table}_List";
    public string GetProc => $"dbo.sp_{Table}_Get";
    public string SaveProc => $"dbo.sp_{Table}_Save";
    public string DeleteProc => $"dbo.sp_{Table}_Delete";
}
