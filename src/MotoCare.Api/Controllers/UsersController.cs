using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

[Route("api/users")]
public sealed class UsersController(IUserRepository repo, UserService service) : ApiControllerBase
{
    [HttpGet, Permission(AppModules.Users, PermissionActions.View)]
    public async Task<IActionResult> List() => Ok(await repo.ListAsync());

    [HttpPost, Permission(AppModules.Users, PermissionActions.Edit)]
    public async Task<IActionResult> Save(UserSaveDto dto) => Ok(new SavedId(await service.SaveAsync(dto)));

    [HttpGet("roles/{roleId:int}/permissions"), Permission(AppModules.Users, PermissionActions.View)]
    public async Task<IActionResult> Permissions(int roleId) => Ok(await repo.GetPermissionsAsync(roleId));

    [HttpPut("roles/{roleId:int}/permissions"), Permission(AppModules.Users, PermissionActions.Edit)]
    public async Task<IActionResult> SavePermissions(int roleId, List<PermissionDto> list)
    {
        await service.SavePermissionsAsync(roleId, list);
        return NoContent();
    }
}
