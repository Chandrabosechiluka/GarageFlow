using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GarageFlow.Application.Abstractions;
using GarageFlow.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GarageFlow.Infrastructure.Auth;

/// <summary>
/// Creates a signed JWT. A JWT is NOT encrypted: anyone can read the claims. It is signed, so nobody can
/// change them without the secret key. Never put secrets in the claims.
/// </summary>
public class JwtTokenGenerator(IOptions<JwtOptions> options, IClock clock) : IJwtTokenGenerator
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken Generate(AppUser user)
    {
        var now = clock.UtcNow;
        var expires = now.AddMinutes(_options.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.DisplayName),
            new Claim("role", user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
