using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>Login: checks the password and returns a JWT token, the user and the user's permissions.</summary>
public sealed class AuthService(IAuthRepository authRepo, IUserRepository userRepo, IPasswordHasher hasher, ITokenService tokens)
{
    public async Task<object> LoginAsync(LoginRequest request)
    {
        Check.Required(request.Username, "Username");
        Check.Required(request.Password, "Password");

        var user = await authRepo.GetForLoginAsync(request.Username.Trim());

        // same message for "no user" and "wrong password" so usernames cannot be guessed
        Check.That(user is not null && hasher.Verify(request.Password, user.PasswordHash), "Username or password is incorrect.");
        Check.That(user!.IsActive, "This account is disabled. Ask an admin to activate it.");

        await authRepo.TouchLoginAsync(user.UserId);
        var (token, expires) = tokens.CreateToken(user.UserId, user.Username, user.RoleId, user.RoleName);
        var permissions = await userRepo.GetPermissionsAsync(user.RoleId);

        return new
        {
            token,
            expiresAt = expires,
            user = new
            {
                user.UserId,
                user.Username,
                user.RoleId,
                user.RoleName,
                user.EmployeeName,
                user.MaxDiscountPct,
                user.CanOverrideCredit,
                user.CanVoidInvoice,
                user.CanEditPrice,
                user.CanGiveCredit,
                isAdmin = user.RoleName == "Admin"
            },
            permissions
        };
    }
}
