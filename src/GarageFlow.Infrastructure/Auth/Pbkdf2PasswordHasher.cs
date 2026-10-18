using System.Security.Cryptography;
using GarageFlow.Application.Abstractions;

namespace GarageFlow.Infrastructure.Auth;

/// <summary>
/// PBKDF2 with HMAC-SHA256 and a random salt per password. Stored as "v1.iterations.salt.hash" (Base64),
/// so the settings travel with the hash and can be raised later without breaking old passwords.
/// Deliberately slow: that is what makes guessing passwords expensive for an attacker.
/// </summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private readonly int _iterations;

    public Pbkdf2PasswordHasher(int iterations = 600_000)
    {
        _iterations = iterations;
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, HashSize);
        return $"v1.{_iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

            // Constant-time comparison: does not leak how many leading bytes matched.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
