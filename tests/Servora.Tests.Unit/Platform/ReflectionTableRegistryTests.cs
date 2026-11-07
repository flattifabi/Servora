using Servora.Domain.Common;
using Servora.Infrastructure.Platform;

namespace Servora.Tests.Unit.Platform;

/// <summary>Tests for <see cref="ReflectionTableRegistry"/>.</summary>
public sealed class ReflectionTableRegistryTests
{
    [Fact]
    public async Task UserTable_NeverExposesSecrets()
    {
        var table = await new ReflectionTableRegistry().GetTableAsync(TableNames.User);

        Assert.NotNull(table);
        Assert.Null(table.FindField("PasswordHash"));
        Assert.Null(table.FindField("SecurityStamp"));
        Assert.NotNull(table.FindField("DisplayName"));
    }

    [Fact]
    public void Humanize_RemovesIdSuffixAndSplitsWords()
    {
        Assert.Equal("Assignment Group", ReflectionTableRegistry.Humanize("AssignmentGroupId"));
        Assert.Equal("Ip Address", ReflectionTableRegistry.Humanize("IpAddress"));
    }
}
