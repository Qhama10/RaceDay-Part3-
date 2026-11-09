using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Raceday_API__prog_part_2_.Services;

public class JwtSettings
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 120;
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(int userId, string email, string fullName, string role);
}

public class TokenService : ITokenService
{
    private readonly JwtSettings _s;
    public TokenService(JwtSettings s) => _s = s;

    public (string, DateTime) CreateToken(int userId, string email, string fullName, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Role, role)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_s.Key));
        var expires = DateTime.UtcNow.AddMinutes(_s.ExpiryMinutes);
        var token = new JwtSecurityToken(_s.Issuer, _s.Audience, claims, expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}