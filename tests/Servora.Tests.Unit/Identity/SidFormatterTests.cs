using Servora.Infrastructure.Identity;

namespace Servora.Tests.Unit.Identity;

/// <summary>Tests for <see cref="SidFormatter"/>.</summary>
public sealed class SidFormatterTests
{
    [Fact]
    public void Format_ConvertsDomainUserSid()
    {
        // S-1-5-21-1004336348-1177238915-682003330-512 (Domain Admins of a sample domain).
        byte[] sid =
        [
            0x01, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05,
            0x15, 0x00, 0x00, 0x00,
            0xDC, 0xF4, 0xDC, 0x3B,
            0x83, 0x3D, 0x2B, 0x46,
            0x82, 0x8B, 0xA6, 0x28,
            0x00, 0x02, 0x00, 0x00,
        ];

        Assert.Equal("S-1-5-21-1004336348-1177238915-682003330-512", SidFormatter.Format(sid));
    }

    [Fact]
    public void Format_ConvertsWellKnownSid()
    {
        // S-1-5-18 (Local System).
        Assert.Equal("S-1-5-18", SidFormatter.Format([0x01, 0x01, 0, 0, 0, 0, 0, 0x05, 0x12, 0, 0, 0]));
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x01, 0x02, 0, 0, 0, 0, 0, 0x05, 0x12, 0, 0, 0 })]
    [InlineData(new byte[] { 0x02, 0x01, 0, 0, 0, 0, 0, 0x05, 0x12, 0, 0, 0 })]
    public void Format_RejectsMalformedInput(byte[] sid)
    {
        Assert.Null(SidFormatter.Format(sid));
    }
}
