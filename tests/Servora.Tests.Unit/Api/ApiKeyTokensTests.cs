using Servora.Application.Api;

namespace Servora.Tests.Unit.Api;

/// <summary>Tests for <see cref="ApiKeyTokens"/> (format, hashing, constant-time verification).</summary>
public sealed class ApiKeyTokensTests
{
    [Fact]
    public void Generate_ProducesParsableTokenWhoseSecretMatchesTheStoredHash()
    {
        var generated = ApiKeyTokens.Generate();

        Assert.StartsWith("srv_" + generated.KeyId + "_", generated.Token);
        Assert.Equal(ApiKeyTokens.KeyIdLength, generated.KeyId.Length);
        Assert.True(ApiKeyTokens.TryParse(generated.Token, out var parsed));
        Assert.Equal(generated.KeyId, parsed.KeyId);
        Assert.Equal(ApiKeyTokens.SecretLength, parsed.Secret.Length);
        Assert.True(ApiKeyTokens.Verify(parsed.Secret, generated.SecretHash));
    }

    [Fact]
    public void Generate_NeverStoresTheSecretItself()
    {
        var generated = ApiKeyTokens.Generate();
        ApiKeyTokens.TryParse(generated.Token, out var parsed);

        Assert.DoesNotContain(parsed!.Secret, generated.SecretHash, StringComparison.Ordinal);
        Assert.Equal(64, generated.SecretHash.Length);
        Assert.Equal(ApiKeyTokens.HashSecret(parsed.Secret), generated.SecretHash);
    }

    [Fact]
    public void Generate_IsRandom()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => ApiKeyTokens.Generate().Token).ToHashSet();

        Assert.Equal(50, tokens.Count);
    }

    [Fact]
    public void GenerateForKeyId_KeepsTheKeyIdButChangesTheSecret()
    {
        var first = ApiKeyTokens.Generate();
        var rotated = ApiKeyTokens.GenerateForKeyId(first.KeyId);

        Assert.Equal(first.KeyId, rotated.KeyId);
        Assert.NotEqual(first.Token, rotated.Token);
        Assert.NotEqual(first.SecretHash, rotated.SecretHash);
    }

    [Fact]
    public void Verify_RejectsWrongSecretsAndMalformedHashes()
    {
        var generated = ApiKeyTokens.Generate();
        ApiKeyTokens.TryParse(generated.Token, out var parsed);
        var tampered = (parsed!.Secret[0] == 'a' ? 'b' : 'a') + parsed.Secret[1..];

        Assert.False(ApiKeyTokens.Verify(tampered, generated.SecretHash));
        Assert.False(ApiKeyTokens.Verify(string.Empty, generated.SecretHash));
        Assert.False(ApiKeyTokens.Verify(parsed.Secret, "not-a-hash"));
        Assert.False(ApiKeyTokens.Verify(parsed.Secret, new string('z', 64)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("srv_")]
    [InlineData("abc_123456789012_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("srv_12345678901_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("srv_ABCDEFGHIJKL_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("srv_abcdefghijkl_short")]
    [InlineData("srv_abcdefghijkl_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!")]
    public void TryParse_RejectsMalformedTokens(string? token)
    {
        Assert.False(ApiKeyTokens.TryParse(token, out _));
        Assert.Null(ApiKeyTokens.TryGetKeyId(token));
    }

    [Fact]
    public void TryParse_RejectsOverlongInput()
    {
        Assert.False(ApiKeyTokens.TryParse("srv_" + new string('a', 500), out _));
    }

    [Fact]
    public void Mask_HidesTheSecret()
    {
        Assert.Equal("srv_abcdefghijkl_••••••••", ApiKeyTokens.Mask("abcdefghijkl"));
    }
}
