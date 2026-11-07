using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Configuration;
using Servora.Application.Notifications;
using Servora.Infrastructure.Configuration;

namespace Servora.Tests.Unit.Configuration;

/// <summary>Tests the real System.Net.Mail based <see cref="SmtpTransport"/> against a loopback SMTP server.</summary>
public sealed class SmtpTransportTests
{
    private static SmtpConnectionOptions Options(int port, TimeSpan? timeout = null) => new()
    {
        Host = "127.0.0.1",
        Port = port,
        Security = SmtpSecurityMode.None,
        FromAddress = "servora@example.com",
        FromName = "Servora",
        Timeout = timeout ?? TimeSpan.FromSeconds(10),
    };

    [Fact]
    public async Task SendAsync_DeliversMessageToSmtpServer()
    {
        await using var server = new LoopbackSmtpServer();
        var transport = new SmtpTransport(NullLogger<SmtpTransport>.Instance);
        var message = new EmailMessage(["alice@example.com"], "Servora test e-mail", "<p>Hello from Servora</p>")
        {
            Cc = ["bob@example.com"],
            TextBody = "Hello from Servora",
        };

        var result = await transport.SendAsync(Options(server.Port), message);

        Assert.True(result.Succeeded, result.Error);
        Assert.True(server.Senders.TryDequeue(out var sender));
        Assert.Equal("servora@example.com", sender);
        Assert.True(server.Recipients.TryDequeue(out var recipients));
        Assert.Equal(["alice@example.com", "bob@example.com"], recipients.Order(StringComparer.Ordinal));
        Assert.True(server.Messages.TryDequeue(out var data));
        Assert.Contains("alice@example.com", data, StringComparison.Ordinal);
        Assert.Contains("multipart/alternative", data, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_ReturnsServerReply_WhenRecipientIsRejected()
    {
        await using var server = new LoopbackSmtpServer("nobody@example.com");
        var transport = new SmtpTransport(NullLogger<SmtpTransport>.Instance);

        var result = await transport.SendAsync(Options(server.Port), new EmailMessage(["nobody@example.com"], "Test", "<p>x</p>"));

        Assert.False(result.Succeeded);
        Assert.Contains("SMTP", result.Error, StringComparison.Ordinal);
        Assert.Empty(server.Messages);
    }

    [Fact]
    public async Task SendAsync_FailsGracefully_WhenServerIsUnreachable()
    {
        // Reserve a free port and close it again so nothing listens there.
        int port;
        await using (var server = new LoopbackSmtpServer())
        {
            port = server.Port;
        }

        var transport = new SmtpTransport(NullLogger<SmtpTransport>.Instance);
        var options = new SmtpConnectionOptions
        {
            Host = "127.0.0.1",
            Port = port,
            Security = SmtpSecurityMode.None,
            FromAddress = "servora@example.com",
            UserName = "mailer",
            Password = "s3cret-password",
            Timeout = TimeSpan.FromSeconds(5),
        };

        var result = await transport.SendAsync(options, new EmailMessage(["alice@example.com"], "Test", "<p>x</p>"));

        Assert.False(result.Succeeded);
        Assert.DoesNotContain("s3cret-password", result.Error ?? string.Empty, StringComparison.Ordinal);
    }
}
