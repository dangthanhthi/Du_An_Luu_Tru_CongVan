using System.Text;
using System.Text.Json;
using EmailWorkerService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

public class ImapConnectionTestTests
{
    [Theory]
    [InlineData("{}", "imapHost")]
    [InlineData("null", "body")]
    [InlineData("{", "Invalid JSON")]
    [InlineData("{\"imapPort\":\"993\"}", "number")]
    [InlineData("{\"useSsl\":\"true\"}", "boolean")]
    [InlineData("{\"host\":\"imap.gmail.com\",\"port\":993,\"username\":\"a@gmail.com\",\"password\":\"secret-marker\"}", "imapHost")]
    [InlineData("{\"imapHost\":\"imap.gmail.com\",\"imapPort\":993,\"useSsl\":true,\"emailAddress\":\"a@gmail.com\"}", "appPassword")]
    [InlineData("{\"imapHost\":\"imap.gmail.com\",\"imapPort\":0,\"useSsl\":true,\"emailAddress\":\"a@gmail.com\",\"appPassword\":\"secret-marker\"}", "imapPort")]
    public async Task InvalidRequestsReturnSafeValidationErrors(string json, string expected)
    {
        var result = await Run(json);
        Assert.Equal(400, result.Status);
        Assert.Contains(expected, result.Body);
        Assert.DoesNotContain("secret-marker", result.Body);
    }

    [Fact]
    public async Task WrongContentTypeReturnsClearError()
    {
        var result = await Run("{}", "text/plain");
        Assert.Equal(400, result.Status);
        Assert.Contains("Content-Type", result.Body);
    }

    [Fact]
    public async Task MinimalPayloadReachesConnectionWithoutWhitelistOrInterval()
    {
        // Reserve then close a local port to exercise connection refusal without contacting Gmail.
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var result = await Run(JsonSerializer.Serialize(new {
            imapHost = "127.0.0.1", imapPort = port, useSsl = false,
            emailAddress = "test@example.com", appPassword = "secret-marker"
        }));
        Assert.Equal(502, result.Status);
        Assert.Contains("IMAP_CONNECTION_FAILED", result.Body);
        Assert.DoesNotContain("secret-marker", result.Body);
    }

    [Theory]
    [InlineData(true, 200, "successful")]
    [InlineData(false, 422, "IMAP_AUTH_FAILED")]
    public async Task MailboxAuthenticationHasDistinctResult(bool accept, int status, string message)
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(deadline.Token);
            using var stream = socket.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("* OK [CAPABILITY IMAP4rev1] Test server ready");
            while (await reader.ReadLineAsync(deadline.Token) is { } line)
            {
                var tag = line.Split(' ')[0];
                if (line.Contains("CAPABILITY"))
                {
                    await writer.WriteLineAsync("* CAPABILITY IMAP4rev1");
                    await writer.WriteLineAsync($"{tag} OK capability");
                }
                else if (line.Contains("LOGIN"))
                    await writer.WriteLineAsync(accept ? $"{tag} OK authenticated" : $"{tag} NO secret-marker rejected");
                else if (line.Contains("LOGOUT"))
                {
                    await writer.WriteLineAsync("* BYE closing");
                    await writer.WriteLineAsync($"{tag} OK logout");
                    break;
                }
                else
                    await writer.WriteLineAsync($"{tag} OK completed");
            }
        });
        var result = await Run(JsonSerializer.Serialize(new {
            imapHost = "127.0.0.1", imapPort = port, useSsl = false,
            emailAddress = "test@example.com", appPassword = "secret-marker"
        }));
        await server;
        Assert.Equal(status, result.Status);
        Assert.Contains(message, result.Body);
        Assert.DoesNotContain("secret-marker", result.Body);
    }

    private static async Task<(int? Status, string Body)> Run(string json, string contentType = "application/json")
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var store = new EmailScannerStore(new ConfigurationBuilder().Build());
        var result = await ImapConnectionTest.HandleAsync(context.Request, store);
        return (((IStatusCodeHttpResult)result).StatusCode,
            JsonSerializer.Serialize(((IValueHttpResult)result).Value));
    }
}
