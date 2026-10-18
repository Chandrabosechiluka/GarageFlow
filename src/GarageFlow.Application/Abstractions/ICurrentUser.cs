namespace GarageFlow.Application.Abstractions;

/// <summary>
/// Who is making the current request. The Api fills it from the JWT. Outside a request (the background worker, tests,
/// the seeder) there is nobody, and the audit trail records "system".
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
}
