using System.Security.Claims;
using MotoCare.Application.Abstractions;
using MotoCare.Infrastructure.Security;

namespace MotoCare.Api.Infrastructure;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();
    public int UserId => int.TryParse(User.FindFirstValue(AppClaims.UserId), out var id) ? id : 0;
    public int RoleId => int.TryParse(User.FindFirstValue(AppClaims.RoleId), out var id) ? id : 0;
    public string RoleName => User.FindFirstValue(ClaimTypes.Role) ?? "";
    public bool IsAdmin => RoleName == "Admin";
}
