using System.IdentityModel.Tokens.Jwt;
using System.Text;
using GarageFlow.Application.Abstractions;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using GarageFlow.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GarageFlow.Infrastructure.Tests;

public class JwtTokenGeneratorTests
{
    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;
    }

    private static readonly DateTime Now = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    private static readonly JwtOptions Options = new()
    {
        Issuer = "GarageFlow.Tests",
        Audience = "GarageFlow.Tests.Api",
        SigningKey = new string('k', 48),
        ExpiryMinutes = 30
    };

    private static JwtTokenGenerator Generator() => new(Microsoft.Extensions.Options.Options.Create(Options), new FixedClock(Now));

    [Fact]
    public void Generate_PutsIdentityAndRoleInTheClaims()
    {
        var user = new AppUser("advisor@example.com", "Asha Advisor", "hash", UserRole.Advisor);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(Generator().Generate(user).Token);

        Assert.Equal(user.Id.ToString(), token.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal("advisor@example.com", token.Claims.Single(c => c.Type == "email").Value);
        Assert.Equal("Asha Advisor", token.Claims.Single(c => c.Type == "name").Value);
        Assert.Equal("Advisor", token.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal("GarageFlow.Tests", token.Issuer);
    }

    [Fact]
    public void Generate_ExpiresAfterTheConfiguredMinutes()
    {
        var user = new AppUser("a@b.com", "Name", "hash", UserRole.Admin);

        var result = Generator().Generate(user);

        Assert.Equal(Now.AddMinutes(30), result.ExpiresAtUtc);
    }

    [Fact]
    public void Generate_TokenIsAcceptedWithTheRightKey_AndRejectedWithAWrongOne()
    {
        var user = new AppUser("a@b.com", "Name", "hash", UserRole.Admin);
        var token = Generator().Generate(user).Token;

        TokenValidationParameters Parameters(string key) => new()
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = false // the fixed test clock is not "now"
        };

        var handler = new JwtSecurityTokenHandler();

        handler.ValidateToken(token, Parameters(Options.SigningKey), out _); // does not throw
        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(token, Parameters(new string('x', 48)), out _));
    }
}
