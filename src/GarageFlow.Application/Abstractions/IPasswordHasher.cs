namespace GarageFlow.Application.Abstractions;

public interface IPasswordHasher
{
    /// <summary>Returns a salted, slow hash that is safe to store. The password itself is never stored.</summary>
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
