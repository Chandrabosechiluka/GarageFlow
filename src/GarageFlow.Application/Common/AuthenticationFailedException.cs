namespace GarageFlow.Application.Common;

/// <summary>
/// Login failed. The message is deliberately the same for "no such user", "wrong password" and
/// "deactivated", so an attacker cannot learn which emails exist. The API maps this to HTTP 401.
/// </summary>
public class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException() : base("Invalid email or password.")
    {
    }
}
