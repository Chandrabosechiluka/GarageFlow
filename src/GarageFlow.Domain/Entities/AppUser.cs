using GarageFlow.Domain.Common;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Domain.Entities;

/// <summary>
/// Someone who can log in to the system. The domain only ever sees the password HASH;
/// hashing itself is a technical detail handled in Infrastructure.
/// </summary>
public class AppUser : BaseEntity
{
    private AppUser()
    {
    }

    public AppUser(string email, string displayName, string passwordHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("A valid email is required.");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("Display name is required.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("A password hash is required.");
        if (!Enum.IsDefined(role))
            throw new DomainException("Unknown role.");

        Email = email.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        PasswordHash = passwordHash;
        Role = role;
    }

    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>A deactivated user can no longer log in. History that mentions them stays intact.</summary>
    public void Deactivate() => IsActive = false;
}
