using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>Staff logins and user type permissions.</summary>
public sealed class UserService(IUserRepository repo, IAuthRepository authRepo, IPasswordHasher hasher, IPermissionService permissions)
{
    public async Task<int> SaveAsync(UserSaveDto dto)
    {
        Check.Required(dto.Username, "Username");
        Check.Required(dto.RoleId, "User type");
        var username = dto.Username.Trim();

        // username must be unique
        var existing = await authRepo.GetForLoginAsync(username);
        Check.That(existing is null || existing.UserId == dto.UserId, "This username is already taken.");

        // password: required for a new user, optional when editing (blank = keep current)
        string? hash = null;
        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            Check.That(dto.Password.Length >= 6, "Password must be at least 6 characters.");
            hash = hasher.Hash(dto.Password);
        }
        else
        {
            Check.That(dto.UserId > 0, "Password is required for a new user.");
        }

        return await repo.SaveAsync(dto.UserId, username, hash, dto.EmployeeId, dto.RoleId, dto.IsActive);
    }

    public async Task SavePermissionsAsync(int roleId, List<PermissionDto>? list)
    {
        Check.That(list is { Count: > 0 }, "No permissions were sent.");
        await repo.SavePermissionsAsync(roleId, list!);
        permissions.Invalidate();
    }
}
