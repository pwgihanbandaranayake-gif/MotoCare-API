using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;
using MotoCare.Application.Services;

namespace MotoCare.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController(AuthService auth, IUserRepository users, ICurrentUser me) : ApiControllerBase
{
    /// <summary>Returns a JWT token. Paste it into Swagger's Authorize box to test other endpoints.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request) => Ok(await auth.LoginAsync(request));

    /// <summary>Permissions of the signed-in user (the React app uses them to show menus and buttons).</summary>
    [HttpGet("my-permissions")]
    public async Task<IActionResult> MyPermissions() => Ok(await users.GetPermissionsAsync(me.RoleId));
}
