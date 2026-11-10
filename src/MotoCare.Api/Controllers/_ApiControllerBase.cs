using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MotoCare.Api.Controllers;

/// <summary>
/// Base for every controller: JSON API, login required.
/// Convention used in all controllers:
///   - GET (read) actions call the repository directly (no rules to apply)
///   - POST / PUT / DELETE actions call a service, which validates the request first
///   - [Permission(module, action)] checks the user type's permissions (Settings > User types)
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Trims a query-string value and turns "" into null.</summary>
    protected static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
