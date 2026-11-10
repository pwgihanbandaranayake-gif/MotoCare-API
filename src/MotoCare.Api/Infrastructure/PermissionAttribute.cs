using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Services;

namespace MotoCare.Api.Infrastructure;

/// <summary>Requires the signed-in user's role to have the given action on the module (dbo.RolePermissions).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PermissionAttribute(string module, string action) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) { context.Result = new UnauthorizedResult(); return; }
        var services = context.HttpContext.RequestServices;
        var user = services.GetRequiredService<ICurrentUser>();
        var permissions = services.GetRequiredService<IPermissionService>();
        if (!await permissions.IsAllowedAsync(user.RoleId, user.RoleName, module, action))
            context.Result = new ObjectResult(new { message = $"Your user type does not have {action.ToLowerInvariant()} access to {module}." })
            { StatusCode = StatusCodes.Status403Forbidden };
    }
}
