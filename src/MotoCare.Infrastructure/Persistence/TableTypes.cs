using System.Data;
using Dapper;
using MotoCare.Application.Dtos;

namespace MotoCare.Infrastructure.Persistence;

/// <summary>
/// Builds table-valued parameters for the SQL table types in 01_schema.sql.
/// IMPORTANT: columns must be added in the SAME ORDER as the CREATE TYPE statement.
/// </summary>
public static class TableTypes
{
    /// <summary>null -> DBNull for DataTable cells.</summary>
    private static object V(object? value) => value ?? DBNull.Value;

    private static DataTable Table(params (string Name, Type Type)[] columns)
    {
        var table = new DataTable();
        foreach (var (name, type) in columns) table.Columns.Add(name, type);
        return table;
    }

    // dbo.PermissionTvp
    public static SqlMapper.ICustomQueryParameter Permissions(IEnumerable<PermissionDto> rows)
    {
        var t = Table(("ModuleKey", typeof(string)), ("CanView", typeof(bool)), ("CanAdd", typeof(bool)),
                      ("CanEdit", typeof(bool)), ("CanDelete", typeof(bool)), ("CanApprove", typeof(bool)));
        foreach (var r in rows) t.Rows.Add(r.ModuleKey, r.CanView, r.CanAdd, r.CanEdit, r.CanDelete, r.CanApprove);
        return t.AsTableValuedParameter("dbo.PermissionTvp");
    }

    // dbo.VehicleTvp
    public static SqlMapper.ICustomQueryParameter Vehicles(IEnumerable<VehicleLineDto> rows)
    {
        var t = Table(("VehicleId", typeof(int)), ("RegNo", typeof(string)), ("ModelId", typeof(int)), ("Year", typeof(int)),
                      ("ChassisNo", typeof(string)), ("EngineNo", typeof(string)), ("Colour", typeof(string)),
                      ("Odometer", typeof(int)), ("ReminderIntervalKm", typeof(int)));
        foreach (var r in rows)
            t.Rows.Add(r.VehicleId, r.RegNo, V(r.ModelId), V(r.Year), V(r.ChassisNo), V(r.EngineNo), V(r.Colour),
                       r.Odometer ?? 0, V(r.ReminderIntervalKm));
        return t.AsTableValuedParameter("dbo.VehicleTvp");
    }

    // dbo.InvoiceItemTvp
    public static SqlMapper.ICustomQueryParameter InvoiceItems(IEnumerable<InvoiceItemDto> rows)
    {
        var t = Table(("Source", typeof(string)), ("FromJob", typeof(bool)), ("ServiceId", typeof(int)), ("PartId", typeof(int)),
                      ("Description", typeof(string)), ("Qty", typeof(decimal)), ("UnitPrice", typeof(decimal)), ("Discount", typeof(decimal)));
        foreach (var r in rows)
            t.Rows.Add(r.Source, r.FromJob, V(r.ServiceId), V(r.PartId), V(r.Description), r.Qty, r.UnitPrice, r.Discount);
        return t.AsTableValuedParameter("dbo.InvoiceItemTvp");
    }

    // dbo.PaymentLineTvp
    public static SqlMapper.ICustomQueryParameter PaymentLines(IEnumerable<PaymentLineDto> rows)
    {
        var t = Table(("PaymentTypeId", typeof(int)), ("Amount", typeof(decimal)), ("Tendered", typeof(decimal)),
                      ("CardTypeId", typeof(int)), ("Last4", typeof(string)), ("ApprovalCode", typeof(string)),
                      ("BankId", typeof(int)), ("ReferenceNo", typeof(string)), ("ChequeDate", typeof(DateTime)));
        foreach (var r in rows)
            t.Rows.Add(r.PaymentTypeId, r.Amount, V(r.Tendered), V(r.CardTypeId), V(Blank(r.Last4)), V(Blank(r.ApprovalCode)),
                       V(r.BankId), V(Blank(r.ReferenceNo)), V(r.ChequeDate?.Date));
        return t.AsTableValuedParameter("dbo.PaymentLineTvp");
    }

    // dbo.POItemTvp
    public static SqlMapper.ICustomQueryParameter PoItems(IEnumerable<PoItemDto> rows)
    {
        var t = Table(("PartId", typeof(int)), ("QtyOrdered", typeof(decimal)), ("UnitPrice", typeof(decimal)));
        foreach (var r in rows) t.Rows.Add(r.PartId, r.QtyOrdered, r.UnitPrice);
        return t.AsTableValuedParameter("dbo.POItemTvp");
    }

    // dbo.GRNItemTvp
    public static SqlMapper.ICustomQueryParameter GrnItems(IEnumerable<GrnItemModel> rows)
    {
        var t = Table(("POItemId", typeof(int)), ("PartId", typeof(int)), ("QtyReceived", typeof(decimal)), ("QtyRejected", typeof(decimal)),
                      ("CostPrice", typeof(decimal)), ("BatchNo", typeof(string)), ("ExpiryDate", typeof(DateTime)));
        foreach (var r in rows)
            t.Rows.Add(V(r.POItemId), r.PartId, r.QtyReceived, r.QtyRejected, r.CostPrice, V(r.BatchNo), V(r.ExpiryDate?.Date));
        return t.AsTableValuedParameter("dbo.GRNItemTvp");
    }

    // dbo.ReturnItemTvp
    public static SqlMapper.ICustomQueryParameter ReturnItems(IEnumerable<ReturnItemModel> rows)
    {
        var t = Table(("PartId", typeof(int)), ("Qty", typeof(decimal)), ("UnitCost", typeof(decimal)));
        foreach (var r in rows) t.Rows.Add(r.PartId, r.Qty, r.UnitCost);
        return t.AsTableValuedParameter("dbo.ReturnItemTvp");
    }

    // ===================================================== Added: new features

    // dbo.StockCountItemTvp
    public static SqlMapper.ICustomQueryParameter StockCountItems(IEnumerable<StockCountLineDto> rows)
    {
        var t = Table(("PartId", typeof(int)), ("CountedQty", typeof(decimal)));
        foreach (var r in rows) t.Rows.Add(r.PartId, V(r.CountedQty));
        return t.AsTableValuedParameter("dbo.StockCountItemTvp");
    }

    // dbo.ServiceCommissionTvp
    public static SqlMapper.ICustomQueryParameter ServiceCommissions(IEnumerable<ServiceCommissionDto> rows)
    {
        var t = Table(("ServiceId", typeof(int)), ("CommissionPct", typeof(decimal)));
        foreach (var r in rows) t.Rows.Add(r.ServiceId, r.CommissionPct ?? 0);
        return t.AsTableValuedParameter("dbo.ServiceCommissionTvp");
    }

    // dbo.EmployeeAllowanceTvp
    public static SqlMapper.ICustomQueryParameter EmployeeAllowances(IEnumerable<EmployeeAllowanceDto> rows)
    {
        var t = Table(("AllowanceTypeId", typeof(int)), ("Amount", typeof(decimal)));
        foreach (var r in rows) t.Rows.Add(r.AllowanceTypeId, r.Amount ?? 0);
        return t.AsTableValuedParameter("dbo.EmployeeAllowanceTvp");
    }

    // dbo.PayrollLineTvp
    public static SqlMapper.ICustomQueryParameter PayrollLines(IEnumerable<PayrollLineModel> rows)
    {
        var t = Table(("EmployeeId", typeof(int)), ("Basic", typeof(decimal)), ("Commission", typeof(decimal)),
                      ("FixedAllowances", typeof(decimal)), ("ExtraAllowances", typeof(decimal)), ("Gross", typeof(decimal)),
                      ("EpfBase", typeof(decimal)), ("EpfEmployee", typeof(decimal)), ("EpfEmployer", typeof(decimal)),
                      ("Etf", typeof(decimal)), ("Deductions", typeof(decimal)), ("NetPay", typeof(decimal)));
        foreach (var r in rows)
            t.Rows.Add(r.EmployeeId, r.Basic, r.Commission, r.FixedAllowances, r.ExtraAllowances, r.Gross,
                       r.EpfBase, r.EpfEmployee, r.EpfEmployer, r.Etf, r.Deductions, r.NetPay);
        return t.AsTableValuedParameter("dbo.PayrollLineTvp");
    }

    // dbo.PayrollDetailTvp
    public static SqlMapper.ICustomQueryParameter PayrollDetails(IEnumerable<PayrollDetailModel> rows)
    {
        var t = Table(("EmployeeId", typeof(int)), ("DetailType", typeof(string)), ("Description", typeof(string)),
                      ("BaseAmount", typeof(decimal)), ("Pct", typeof(decimal)), ("Amount", typeof(decimal)));
        foreach (var r in rows) t.Rows.Add(r.EmployeeId, r.DetailType, r.Description, r.BaseAmount, r.Pct, r.Amount);
        return t.AsTableValuedParameter("dbo.PayrollDetailTvp");
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
