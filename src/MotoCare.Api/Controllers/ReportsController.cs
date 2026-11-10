using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Reports;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

/// <summary>Reports section: list of reports and running one report.</summary>
[Route("api/reports")]
public sealed class ReportsController(ReportService service) : ApiControllerBase
{
    /// <summary>All reports with their filters and columns (the React screen is built from this).</summary>
    [HttpGet, Permission(AppModules.Reports, PermissionActions.View)]
    public async Task<IActionResult> Catalog() => Ok(await service.CatalogAsync());

    /// <summary>Runs one report. Example: GET /api/reports/sales-daily?from=2026-10-01&amp;to=2026-10-31</summary>
    [HttpGet("{key}"), Permission(AppModules.Reports, PermissionActions.View)]
    public async Task<IActionResult> Run(string key, DateTime? from, DateTime? to, int? customerId, int? supplierId, int? top) =>
        Ok(await service.RunAsync(key, from, to, customerId, supplierId, top));
}
