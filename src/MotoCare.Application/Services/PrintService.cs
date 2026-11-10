using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;

namespace MotoCare.Application.Services;

/// <summary>
/// Print / reprint log. The first print of a document is copy 1 (ORIGINAL);
/// every later print is a DUPLICATE COPY with its number, user and time.
/// </summary>
public sealed class PrintService(IPrintRepository repo, ICurrentUser me)
{
    /// <summary>Documents that can be printed. Keys match the React print routes.</summary>
    public static readonly string[] DocTypes = { "INVOICE", "RECEIPT", "JOBCARD", "PO", "GRN", "DEBITNOTE", "SUPPAYMENT",
                                                      "QUOTATION", "PAYSLIP", "STOCKCOUNT" };   // Added: last three

    public Task<RowData?> LogAsync(string docType, int docId)
    {
        string type = Normalise(docType);
        Check.That(docId > 0, "Document id is not valid.");
        return repo.LogAsync(type, docId, me.UserId);
    }

    public Task<List<RowData>> HistoryAsync(string docType, int docId) => repo.HistoryAsync(Normalise(docType), docId);

    private static string Normalise(string docType)
    {
        string type = (docType ?? "").Trim().ToUpperInvariant();
        Check.That(DocTypes.Contains(type), $"'{docType}' is not a printable document.");
        return type;
    }
}
