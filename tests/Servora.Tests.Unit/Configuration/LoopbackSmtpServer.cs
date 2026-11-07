using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Servora.Tests.Unit.Configuration;

/// <summary>
/// Minimal plain-text SMTP server on the loopback interface for transport tests. Supports EHLO/HELO, MAIL, RCPT, DATA,
/// RSET, NOOP and QUIT; recipients listed in the reject set are answered with <c>550</c>. Each accepted message is
/// recorded as "envelope sender | envelope recipients | raw data".
/// </summary>
public sealed class LoopbackSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly HashSet<string> _rejectedRecipients;
    private readonly Task _acceptLoop;

    /// <summary>Initializes a new instance of the <see cref="LoopbackSmtpServer"/> class and starts listening.</summary>
    /// <param name="rejectedRecipients">Recipient addresses answered with <c>550</c>.</param>
    public LoopbackSmtpServer(params string[] rejectedRecipients)
    {
        _rejectedRecipients = new HashSet<string>(rejectedRecipients, StringComparer.OrdinalIgnoreCase);
        _listener.Start();
        _acceptLoop = AcceptLoopAsync();
    }

    /// <summary>Gets the port the server listens on.</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>Gets the envelope sender of every received message.</summary>
    public ConcurrentQueue<string> Senders { get; } = new();

    /// <summary>Gets the envelope recipients of every received message.</summary>
    public ConcurrentQueue<IReadOnlyList<string>> Recipients { get; } = new();

    /// <summary>Gets the raw DATA section of every received message.</summary>
    public ConcurrentQueue<string> Messages { get; } = new();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _acceptLoop;
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
        {
            // Listener stopped.
        }

        _stop.Dispose();
    }

    private static string ExtractAddress(string line)
    {
        var start = line.IndexOf('<', StringComparison.Ordinal);
        var end = line.IndexOf('>', StringComparison.Ordinal);
        return start >= 0 && end > start ? line[(start + 1)..end] : line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(_stop.Token);
            _ = Task.Run(() => HandleAsync(client), _stop.Token);
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("220 localhost Servora test SMTP");

            string? sender = null;
            var recipients = new List<string>();
            while (await reader.ReadLineAsync(_stop.Token) is { } line)
            {
                var command = line.Length >= 4 ? line[..4].ToUpperInvariant() : line.ToUpperInvariant();
                switch (command)
                {
                    case "EHLO":
                        await writer.WriteLineAsync("250-localhost");
                        await writer.WriteLineAsync("250 8BITMIME");
                        break;
                    case "HELO":
                    case "NOOP":
                        await writer.WriteLineAsync("250 OK");
                        break;
                    case "MAIL":
                        sender = ExtractAddress(line);
                        recipients.Clear();
                        await writer.WriteLineAsync("250 OK");
                        break;
                    case "RCPT":
                        var recipient = ExtractAddress(line);
                        if (_rejectedRecipients.Contains(recipient))
                        {
                            await writer.WriteLineAsync("550 5.1.1 Mailbox unavailable");
                        }
                        else
                        {
                            recipients.Add(recipient);
                            await writer.WriteLineAsync("250 OK");
                        }

                        break;
                    case "DATA":
                        await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                        var data = new StringBuilder();
                        while (await reader.ReadLineAsync(_stop.Token) is { } dataLine && dataLine != ".")
                        {
                            data.Append(dataLine.StartsWith("..", StringComparison.Ordinal) ? dataLine[1..] : dataLine).Append("\r\n");
                        }

                        Senders.Enqueue(sender ?? string.Empty);
                        Recipients.Enqueue(recipients.ToList());
                        Messages.Enqueue(data.ToString());
                        await writer.WriteLineAsync("250 OK queued");
                        break;
                    case "RSET":
                        sender = null;
                        recipients.Clear();
                        await writer.WriteLineAsync("250 OK");
                        break;
                    case "QUIT":
                        await writer.WriteLineAsync("221 Bye");
                        return;
                    default:
                        await writer.WriteLineAsync("502 Command not implemented");
                        break;
                }
            }
        }
    }
}
