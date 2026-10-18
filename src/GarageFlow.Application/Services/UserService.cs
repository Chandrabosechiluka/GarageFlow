using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Services;

public interface IUserService
{
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken ct = default);
    Task<UserResponse> DeactivateAsync(Guid id, CancellationToken ct = default);
}

public class UserService(IGarageFlowDbContext db, IPasswordHasher hasher) : IUserService
{
    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException($"A user with email {email} already exists.");

        var user = new AppUser(request.Email, request.DisplayName, hasher.Hash(request.Password), request.Role);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return user.ToResponse();
    }

    public async Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(ct);
        return users.Select(u => u.ToResponse()).ToList();
    }

    public async Task<UserResponse> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(AppUser), id);

        user.Deactivate();
        await db.SaveChangesAsync(ct);

        return user.ToResponse();
    }
}
