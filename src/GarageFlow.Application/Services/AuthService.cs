using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class AuthService(IGarageFlowDbContext db, IPasswordHasher hasher, IJwtTokenGenerator tokens) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

        // One check, one error: the caller never learns WHICH part was wrong.
        if (user is null || !user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
            throw new AuthenticationFailedException();

        var token = tokens.Generate(user);
        return new LoginResponse(token.Token, token.ExpiresAtUtc, user.Role, user.DisplayName);
    }
}
