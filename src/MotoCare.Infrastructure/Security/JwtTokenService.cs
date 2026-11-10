using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MotoCare.Application.Abstractions;

namespace MotoCare.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "MotoCare";
    public string Audience { get; set; } = "MotoCare.Client";
    public string Key { get; set; } = "";
    public int ExpiryHours { get; set; } = 12;
}

public static class AppClaims
{
    public const string UserId = "uid";
    public const string RoleId = "rid";
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public (string Token, DateTime ExpiresAt) CreateToken(int userId, string username, int roleId, string roleName)
    {
        var o = options.Value;
        var expires = DateTime.UtcNow.AddHours(o.ExpiryHours);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(AppClaims.UserId, userId.ToString()),
            new Claim(AppClaims.RoleId, roleId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, roleName)
        };
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
