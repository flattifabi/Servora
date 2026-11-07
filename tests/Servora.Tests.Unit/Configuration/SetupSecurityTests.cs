using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Infrastructure.Configuration;

namespace Servora.Tests.Unit.Configuration;

/// <summary>Tests for the setup token and the local-client policy that protect the setup wizard.</summary>
public sealed class SetupSecurityTests : IDisposable
{
    private readonly TestAppPaths _paths = new();

    [Fact]
    public void Token_IsCreatedOnce_PersistedAndValidated()
    {
        var store = new SetupTokenStore(_paths, NullLogger<SetupTokenStore>.Instance);

        var token = store.EnsureToken();

        Assert.True(token.Length >= 32);
        Assert.True(File.Exists(store.FilePath));
        Assert.Contains(token, File.ReadAllText(store.FilePath));
        Assert.Equal(token, store.EnsureToken());
        Assert.True(store.Validate(token));
        Assert.True(store.Validate($"  {token} "));
        Assert.False(store.Validate(token.ToUpperInvariant() == token ? token + "x" : token.ToUpperInvariant()));
        Assert.False(store.Validate(null));
        Assert.False(store.Validate(string.Empty));
    }

    [Fact]
    public void Token_SurvivesRestart_AndIsDeletedAfterSetup()
    {
        var token = new SetupTokenStore(_paths, NullLogger<SetupTokenStore>.Instance).EnsureToken();
        var restarted = new SetupTokenStore(_paths, NullLogger<SetupTokenStore>.Instance);

        Assert.True(restarted.Validate(token));

        restarted.Delete();
        Assert.False(File.Exists(restarted.FilePath));
        Assert.False(restarted.Validate(token));
    }

    [Theory]
    [InlineData("127.0.0.1", "127.0.0.1", false, true)]
    [InlineData("::1", "::1", false, true)]
    [InlineData("::ffff:127.0.0.1", "::ffff:127.0.0.1", false, true)]
    [InlineData("192.168.1.20", "192.168.1.20", false, true)]
    [InlineData("192.168.1.30", "192.168.1.20", false, false)]
    [InlineData("127.0.0.1", "127.0.0.1", true, false)]
    public void LocalClientPolicy(string remote, string local, bool forwarded, bool expected) =>
        Assert.Equal(expected, SetupAccessPolicy.IsLocalClient(IPAddress.Parse(remote), IPAddress.Parse(local), forwarded));

    [Fact]
    public void UnknownRemoteAddress_IsNotLocal() => Assert.False(SetupAccessPolicy.IsLocalClient(null, IPAddress.Loopback, false));

    /// <inheritdoc />
    public void Dispose() => _paths.Dispose();
}
