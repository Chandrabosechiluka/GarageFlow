using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Application.Services;
using GarageFlow.Domain.Common;
using GarageFlow.Domain.Enums;
using GarageFlow.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GarageFlow.Application.Tests;

public class AuthServiceTests
{
    private static readonly JwtOptions JwtSettings = new()
    {
        Issuer = "GarageFlow.Tests",
        Audience = "GarageFlow.Tests.Api",
        SigningKey = new string('k', 48),
        ExpiryMinutes = 30
    };

    private sealed class Kit
    {
        public Scenario Scenario { get; } = new();
        public Pbkdf2PasswordHasher Hasher { get; } = new(iterations: 1_000);
        public UserService Users { get; }
        public AuthService Auth { get; }

        public Kit()
        {
            Users = new UserService(Scenario.Db, Hasher);
            var tokens = new JwtTokenGenerator(Options.Create(JwtSettings), Scenario.Clock);
            Auth = new AuthService(Scenario.Db, Hasher, tokens);
        }
    }

    [Fact]
    public async Task CreateUser_StoresAHashNotThePassword()
    {
        var kit = new Kit();

        await kit.Users.CreateAsync(new CreateUserRequest("new@example.com", "New Person", "Pa55word!", UserRole.Advisor));

        var stored = await kit.Scenario.Db.Users.SingleAsync();
        Assert.NotEqual("Pa55word!", stored.PasswordHash);
        Assert.DoesNotContain("Pa55word!", stored.PasswordHash);
        Assert.True(kit.Hasher.Verify("Pa55word!", stored.PasswordHash));
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_IgnoringCase_IsAConflict()
    {
        var kit = new Kit();
        await kit.Users.CreateAsync(new CreateUserRequest("new@example.com", "New Person", "Pa55word!", UserRole.Advisor));

        await Assert.ThrowsAsync<ConflictException>(() =>
            kit.Users.CreateAsync(new CreateUserRequest("NEW@example.com", "Someone Else", "Pa55word!", UserRole.Technician)));
    }

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsATokenAndTheRole()
    {
        var kit = new Kit();
        await kit.Users.CreateAsync(new CreateUserRequest("admin@example.com", "Admin Person", "Pa55word!", UserRole.Admin));

        var result = await kit.Auth.LoginAsync(new LoginRequest("Admin@Example.com", "Pa55word!"));

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal(UserRole.Admin, result.Role);
        Assert.Equal("Bearer", result.TokenType);
    }

    [Fact]
    public async Task Login_WrongPassword_Fails()
    {
        var kit = new Kit();
        await kit.Users.CreateAsync(new CreateUserRequest("admin@example.com", "Admin Person", "Pa55word!", UserRole.Admin));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            kit.Auth.LoginAsync(new LoginRequest("admin@example.com", "wrong-password")));
    }

    [Fact]
    public async Task Login_UnknownEmail_FailsWithTheSameError()
    {
        var kit = new Kit();

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            kit.Auth.LoginAsync(new LoginRequest("nobody@example.com", "Pa55word!")));

        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task Login_DeactivatedUser_Fails()
    {
        var kit = new Kit();
        var created = await kit.Users.CreateAsync(new CreateUserRequest("gone@example.com", "Gone Person", "Pa55word!", UserRole.Technician));
        await kit.Users.DeactivateAsync(created.Id);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            kit.Auth.LoginAsync(new LoginRequest("gone@example.com", "Pa55word!")));
    }

    [Fact]
    public async Task Deactivate_UnknownUser_ThrowsNotFound()
    {
        var kit = new Kit();

        await Assert.ThrowsAsync<NotFoundException>(() => kit.Users.DeactivateAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateUser_UnknownRole_IsRejectedByTheDomain()
    {
        var kit = new Kit();

        await Assert.ThrowsAsync<DomainException>(() =>
            kit.Users.CreateAsync(new CreateUserRequest("x@example.com", "X", "Pa55word!", (UserRole)99)));
    }
}
