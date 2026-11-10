using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;

namespace MotoCare.Infrastructure.Persistence.Repositories;

// Purchase orders, GRN, returns, supplier bills and supplier payments.

public sealed class PurchasingRepository(DbExecutor db) : IPurchasingRepository
{
    // ---------- purchase orders
    public Task<List<RowData>> PoListAsync(string? status, int? supplierId) =>
        db.ListAsync("dbo.sp_PO_List", new { Status = status, SupplierId = supplierId });

    public Task<ResultBag> PoGetAsync(int id) =>
        db.MultiAsync("dbo.sp_PO_Get", new { Id = id }, "po?", "items");

    public Task<int> PoSaveAsync(PurchaseOrderSaveDto d, List<PoItemDto> items, int userId) =>
        db.SaveAsync("dbo.sp_PO_Save", new
        {
            d.POId, d.SupplierId, PODate = d.PODate.Date, ExpectedDate = d.ExpectedDate?.Date, UserId = userId,
            Items = TableTypes.PoItems(items)
        });

    public Task PoSetStatusAsync(int poId, string status, int userId) =>
        db.ExecAsync("dbo.sp_PO_SetStatus", new { POId = poId, Status = status, ApprovedBy = userId });

    public Task<List<RowData>> LowStockAsync() => db.ListAsync("dbo.sp_PO_LowStock");

    // ---------- GRN
    public Task<List<RowData>> GrnListAsync(string? status, int? supplierId) =>
        db.ListAsync("dbo.sp_GRN_List", new { Status = status, SupplierId = supplierId });

    public Task<ResultBag> GrnGetAsync(int id) =>
        db.MultiAsync("dbo.sp_GRN_Get", new { Id = id }, "grn?", "items", "returns");

    public Task<List<RowData>> GrnPendingFromPoAsync(int poId) =>
        db.ListAsync("dbo.sp_GRN_PendingFromPO", new { POId = poId });

    public Task<int> GrnSaveAsync(GrnSaveModel m) =>
        db.SaveAsync("dbo.sp_GRN_Save", new
        {
            m.GRNId, m.POId, m.SupplierId, m.GRNDate, m.UserId,
            Items = TableTypes.GrnItems(m.Items)
        });

    public Task GrnPostAsync(int grnId) => db.ExecAsync("dbo.sp_GRN_Post", new { GRNId = grnId });

    public Task GrnDeleteAsync(int grnId) => db.ExecAsync("dbo.sp_GRN_Delete", new { GRNId = grnId });

    // ---------- returns
    public Task<int> ReturnSaveAsync(int grnId, DateTime returnDate, string reason, List<ReturnItemModel> items) =>
        db.SaveAsync("dbo.sp_GRNReturn_Save", new
        {
            GRNId = grnId, ReturnDate = returnDate, Reason = reason,
            Items = TableTypes.ReturnItems(items)
        });

    public Task<List<RowData>> ReturnListAsync() => db.ListAsync("dbo.sp_GRNReturn_List");
}

public sealed class SupplierBillRepository(DbExecutor db) : ISupplierBillRepository
{
    public Task<List<RowData>> ListAsync(int? supplierId, string? status) =>
        db.ListAsync("dbo.sp_SupplierBill_List", new { SupplierId = supplierId, Status = status });

    public Task<RowData?> GetAsync(int billId) =>
        db.SingleAsync("dbo.sp_SupplierBill_Get", new { BillId = billId });

    public Task<List<RowData>> UnbilledGrnAsync(int? supplierId) =>
        db.ListAsync("dbo.sp_GRN_Unbilled", new { SupplierId = supplierId });

    public Task<int> SaveAsync(int supplierId, int? grnId, string billNo, DateTime billDate, DateTime dueDate, decimal amount) =>
        db.SaveAsync("dbo.sp_SupplierBill_Save", new
        {
            SupplierId = supplierId, GRNId = grnId, BillNo = billNo, BillDate = billDate, DueDate = dueDate, Amount = amount
        });

    public Task<int> PayAsync(SupplierPaymentDto d, int userId) =>
        db.SaveAsync("dbo.sp_SupplierPayment_Save", new
        {
            d.BillId, PayDate = d.PayDate.Date, d.PaymentTypeId, d.Amount, d.BankId, d.ReferenceNo, UserId = userId
        });

    public Task<List<RowData>> PaymentsAsync(int? supplierId) =>
        db.ListAsync("dbo.sp_SupplierPayment_List", new { SupplierId = supplierId });
}
