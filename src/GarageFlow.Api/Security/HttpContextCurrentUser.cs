using GarageFlow.Application.Abstractions;

namespace GarageFlow.Api.Security;

/// <summary>Reads the caller's identity from the validated JWT ("sub" and "email" claims). No request, no user.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? Email => Principal?.FindFirst("email")?.Value;
}
