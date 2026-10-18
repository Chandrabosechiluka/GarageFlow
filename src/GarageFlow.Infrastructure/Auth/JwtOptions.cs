namespace GarageFlow.Infrastructure.Auth;

/// <summary>Bound from the "Jwt" section of configuration. The SigningKey comes from user secrets, never from Git.</summary>
public class JwtOptions
{
    public string Issuer { get; set; } = "GarageFlow";
    public string Audience { get; set; } = "GarageFlow.Api";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}
