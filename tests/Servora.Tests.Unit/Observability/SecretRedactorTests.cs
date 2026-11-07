using Servora.Infrastructure.Observability.Logging;

namespace Servora.Tests.Unit.Observability;

/// <summary>Tests for <see cref="SecretRedactor"/>.</summary>
public sealed class SecretRedactorTests
{
    [Theory]
    [InlineData("Server=db;User Id=sa;Password=Sup3r$ecret;Encrypt=true", "Sup3r$ecret")]
    [InlineData("Host=pg;Username=app;Pwd=hunter2", "hunter2")]
    [InlineData("{\"userName\":\"bob\",\"password\":\"p@ss \\\"word\\\"\"}", "p@ss")]
    [InlineData("{\"clientSecret\": \"abc123xyz\"}", "abc123xyz")]
    [InlineData("Authorization: Bearer eyAbCdEf0123456789.payload.sig", "eyAbCdEf0123456789")]
    [InlineData("Basic dXNlcjpwYXNzd29yZA==", "dXNlcjpwYXNzd29yZA")]
    [InlineData("calling https://admin:TopSecret@api.example.com/v1", "TopSecret")]
    [InlineData("token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U issued", "dozjgNryP4J3jVmNHl0w5N")]
    [InlineData("?api_key=AKIA1234567890&page=2", "AKIA1234567890")]
    [InlineData("apiKey: 'k-998877'", "k-998877")]
    [InlineData("UserPassword=letmein;", "letmein")]
    public void Redact_RemovesSecrets(string input, string secret)
    {
        var redacted = SecretRedactor.Redact(input)!;

        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        Assert.Contains(SecretRedactor.Mask, redacted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("User admin signed in from 10.0.0.1")]
    [InlineData("Processed 12 tokens: done")]
    [InlineData("Server=db;Database=servora;Trusted_Connection=True")]
    public void Redact_KeepsHarmlessText(string input) => Assert.Equal(input, SecretRedactor.Redact(input));

    [Fact]
    public void Redact_KeepsNonSecretPartsOfConnectionStrings()
    {
        var redacted = SecretRedactor.Redact("Server=db01;Database=servora;User Id=sa;Password=x1;")!;

        Assert.Contains("Server=db01", redacted, StringComparison.Ordinal);
        Assert.Contains("User Id=sa", redacted, StringComparison.Ordinal);
        Assert.Contains("Password=***", redacted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Password", true)]
    [InlineData("newPassword", true)]
    [InlineData("ClientSecret", true)]
    [InlineData("api_key", true)]
    [InlineData("ConnectionString", true)]
    [InlineData("UserName", false)]
    [InlineData("Count", false)]
    public void IsSensitiveKey_DetectsSecretPropertyNames(string key, bool expected) => Assert.Equal(expected, SecretRedactor.IsSensitiveKey(key));
}
