namespace MotoCare.Application.Abstractions;

public interface ICurrentUser
{
    int UserId { get; }
    int RoleId { get; }
    string RoleName { get; }
    bool IsAdmin { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(int userId, string username, int roleId, string roleName);
}
