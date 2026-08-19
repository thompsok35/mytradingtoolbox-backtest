using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MyTradingToolbox.Core.Entities;

namespace MyTradingToolbox.Services.Auth;

public interface IJwtTokenService
{
    string GenerateUserToken(AppUser user);
    string GenerateTwoFactorChallengeToken(AppUser user);
    ClaimsPrincipal? ValidateToken(string token, bool isTwoFactorChallenge = false);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly byte[] _keyBytes;
    private const string Issuer = "MyTradingToolbox-Backtest";
    private const string Audience = "MyTradingToolbox-Users";

    public JwtTokenService(IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"] ?? configuration["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            secret = "MyTradingToolbox_SuperSecret_Jwt_EncryptionKey_2026_SecureKey!";
        }
        _keyBytes = Encoding.UTF8.GetBytes(secret);
    }

    public string GenerateUserToken(AppUser user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("type", "session")
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_keyBytes), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateTwoFactorChallengeToken(AppUser user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("type", "2fa_challenge")
            }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_keyBytes), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token, bool isTwoFactorChallenge = false)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_keyBytes),
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);

            var typeClaim = principal.FindFirst("type")?.Value;
            if (isTwoFactorChallenge && typeClaim != "2fa_challenge") return null;
            if (!isTwoFactorChallenge && typeClaim != "session") return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
