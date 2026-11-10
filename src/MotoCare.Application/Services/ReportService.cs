using System.Text.Json;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Reports;

namespace MotoCare.Application.Services;

/// <summary>
/// Runs a report from the catalogue: checks the filters, then calls its sp_Rpt_* procedure
/// with only the parameters that report accepts.
/// </summary>
public sealed class ReportService(IReportRepository repo, IPermissionService permissions, ICurrentUser me)
{
    private const int MaxDays = 366 * 3;   // longest period one report may cover

    public async Task<object> RunAsync(string key, DateTime? from, DateTime? to, int? customerId, int? supplierId, int? top)
    {
        var report = ReportCatalog.Find(key) ?? throw new KeyNotFoundException($"Report '{key}' not found.");
        Check.That(await CanSeeAsync(report), "Payroll reports need the Payroll > View permission.");

        // ---- 1. dates (default: this month)
        DateTime toDate = (to ?? DateTime.Today).Date;
        DateTime fromDate = (from ?? new DateTime(toDate.Year, toDate.Month, 1)).Date;
        bool usesDates = !report.Filters.HasFlag(ReportFilter.NoDates);
        if (usesDates && !report.Filters.HasFlag(ReportFilter.AsAtDate))
        {
            Check.That(fromDate <= toDate, "'From' date must be on or before the 'To' date.");
            Check.That((toDate - fromDate).TotalDays <= MaxDays, "Choose a period of 3 years or less.");
        }

        // ---- 2. build only the parameters this procedure has
        var parameters = new Dictionary<string, object?>();
        if (usesDates)
        {
            if (!report.Filters.HasFlag(ReportFilter.AsAtDate)) parameters["From"] = fromDate;
            parameters["To"] = toDate;
        }
        if (report.Filters.HasFlag(ReportFilter.Customer)) parameters["CustomerId"] = customerId is > 0 ? customerId : null;
        if (report.Filters.HasFlag(ReportFilter.Supplier)) parameters["SupplierId"] = supplierId is > 0 ? supplierId : null;
        if (report.Filters.HasFlag(ReportFilter.Top))
        {
            int topN = top ?? 20;
            Check.That(topN is >= 1 and <= 500, "Top must be between 1 and 500.");
            parameters["Top"] = topN;
        }

        // ---- 3. run
        var rows = await repo.RunAsync(report.Procedure, parameters);
        return new { report = Describe(report), from = fromDate, to = toDate, rows };
    }

    /// <summary>Added: salary reports are only for users who can view payroll.</summary>
    public async Task<bool> CanSeeAsync(ReportDefinition r) =>
        r.Group != "Payroll" || await permissions.IsAllowedAsync(me.RoleId, me.RoleName, "Payroll", "View");

    /// <summary>Reports this user may run.</summary>
    public async Task<List<object>> CatalogAsync()
    {
        var list = new List<object>();
        foreach (var r in ReportCatalog.All) if (await CanSeeAsync(r)) list.Add(Describe(r));
        return list;
    }

    /// <summary>Catalogue entry as sent to the React app (no procedure name).</summary>
    public static object Describe(ReportDefinition r) => new
    {
        r.Key, r.Title, r.Group, r.Description,
        filters = new
        {
            customer = r.Filters.HasFlag(ReportFilter.Customer),
            supplier = r.Filters.HasFlag(ReportFilter.Supplier),
            top = r.Filters.HasFlag(ReportFilter.Top),
            asAtDate = r.Filters.HasFlag(ReportFilter.AsAtDate),
            noDates = r.Filters.HasFlag(ReportFilter.NoDates),
        },
        // same camelCase rule the API uses for row keys (GRNNo -> grnNo), so names match the rows
        columns = r.Columns.Select(c => new { name = JsonNamingPolicy.CamelCase.ConvertName(c.Name), c.Label, format = c.Format.ToString().ToLowerInvariant(), c.Total })
    };
}
