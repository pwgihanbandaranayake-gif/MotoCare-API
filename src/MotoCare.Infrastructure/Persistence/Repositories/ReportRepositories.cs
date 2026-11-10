using Dapper;
using MotoCare.Application.Abstractions;

namespace MotoCare.Infrastructure.Persistence.Repositories;

// Reports (sp_Rpt_*) and print log (sp_Print_*).

public sealed class ReportRepository(DbExecutor db) : IReportRepository
{
    public Task<List<RowData>> RunAsync(string procedure, IReadOnlyDictionary<string, object?> parameters)
    {
        var p = new DynamicParameters();
        foreach (var (name, value) in parameters) p.Add(name, value);
        return db.ListAsync(procedure, p);
    }
}

public sealed class PrintRepository(DbExecutor db) : IPrintRepository
{
    public Task<RowData?> LogAsync(string docType, int docId, int userId) =>
        db.SingleAsync("dbo.sp_Print_Log", new { DocType = docType, DocId = docId, UserId = userId });

    public Task<List<RowData>> HistoryAsync(string docType, int docId) =>
        db.ListAsync("dbo.sp_Print_History", new { DocType = docType, DocId = docId });

    public Task<ResultBag> DebitNoteAsync(int returnId) =>
        db.MultiAsync("dbo.sp_GRNReturn_Get", new { Id = returnId }, "note?", "items");

    public Task<RowData?> SupplierPaymentAsync(int supPaymentId) =>
        db.SingleAsync("dbo.sp_SupplierPayment_Get", new { Id = supPaymentId });
}
