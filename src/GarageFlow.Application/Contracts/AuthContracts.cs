using System.ComponentModel.DataAnnotations;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Application.Contracts;

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, UserRole Role, string DisplayName)
{
    /// <summary>Send the token in the header: Authorization: Bearer {AccessToken}</summary>
    public string TokenType => "Bearer";
}

public record CreateUserRequest(
    [property: Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(200)] string DisplayName,
    [property: Required, StringLength(100, MinimumLength = 8)] string Password,
    [property: EnumDataType(typeof(UserRole))] UserRole Role);

public record UserResponse(Guid Id, string Email, string DisplayName, UserRole Role, bool IsActive);
