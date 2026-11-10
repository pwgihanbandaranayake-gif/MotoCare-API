using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

[Route("api/dashboard")]
public sealed class DashboardController(IDashboardRepository repo) : ApiControllerBase
{
    [HttpGet, Permission(AppModules.Dashboard, PermissionActions.View)]
    public async Task<IActionResult> Get() => Ok(await repo.GetAsync());
}
