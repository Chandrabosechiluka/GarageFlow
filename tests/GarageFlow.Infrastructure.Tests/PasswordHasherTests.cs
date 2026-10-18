using GarageFlow.Infrastructure.Auth;

namespace GarageFlow.Infrastructure.Tests;

public class PasswordHasherTests
{
    // A low iteration count keeps the tests fast; production uses the default (600,000).
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 1_000);

    [Fact]
    public void Hash_DoesNotContainThePlainPassword()
    {
        var hash = _hasher.Hash("Secret#123");

        Assert.DoesNotContain("Secret#123", hash);
    }

    [Fact]
    public void Hash_SamePasswordTwice_GivesDifferentHashesBecauseOfTheSalt()
    {
        Assert.NotEqual(_hasher.Hash("Secret#123"), _hasher.Hash("Secret#123"));
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("Secret#123");

        Assert.True(_hasher.Verify("Secret#123", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("Secret#123");

        Assert.False(_hasher.Verify("secret#123", hash));
    }

    [Fact]
    public void Verify_HashMadeWithOtherSettings_StillWorks()
    {
        // The iteration count is stored inside the hash, so old hashes keep working if the default is raised later.
        var hash = new Pbkdf2PasswordHasher(iterations: 2_000).Hash("Secret#123");

        Assert.True(_hasher.Verify("Secret#123", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("v1.abc.def.ghi")]
    [InlineData("v1.1000.not-base64.not-base64")]
    public void Verify_MalformedHash_ReturnsFalseInsteadOfThrowing(string malformed)
    {
        Assert.False(_hasher.Verify("Secret#123", malformed));
    }
}
