using GarageFlow.Domain.Entities;

namespace GarageFlow.Application.Abstractions;

public record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface IJwtTokenGenerator
{
    AccessToken Generate(AppUser user);
}
