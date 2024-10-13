using Cute.Constants;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using TextCopy;

namespace Cute.Services.ClipboardWebServer;

public static class ClipboardServer
{
    public static readonly string Endpoint = $"http://localhost:{FindAvailablePort()}";

    public static int FindAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static async Task StartServerAsync(CancellationToken token)
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.None);
                });
                webBuilder.ConfigureServices(services => { });
                webBuilder.Configure(app =>
                {
                    app.Run(async context =>
                    {
                        if (context.Request.Path == "/copy")
                        {
                            var key = context.Request.Query["key"].ToString();
                            var text = PlaceTextOnClipboard(key);
                            if (text is null)
                            {
                                context.Response.StatusCode = 404;
                                await context.Response.WriteAsync("Not Found");
                            }
                            else
                            {
                                await context.Response.WriteAsync(text);
                            }
                        }
                        else
                        {
                            await context.Response.WriteAsync($"{Globals.AppName} Clipboard Server {Globals.AppVersion}.");
                        }
                    });
                })
                .UseUrls(Endpoint);
            })
            .Build();

        try
        {
            await host.RunAsync(token);
        }
        catch (OperationCanceledException)
        {
            // silent;
        }
    }

    private static string? PlaceTextOnClipboard(string key)
    {
        var clipboard = new Clipboard();
        if (_copyTexts.TryGetValue(key, out var text))
        {
            clipboard.SetText(text);
            return text;
        }
        return null;
    }

    private static readonly ConcurrentDictionary<string, string> _copyTexts = new();

    public static string RegisterCopyText(string text)
    {
        var key = Guid.NewGuid().ToString("N")[..8];
        RegisterCopyText(key, text);
        return $"{Endpoint}/copy?key={key}";
    }

    private static void RegisterCopyText(string key, string text)
    {
        _copyTexts.AddOrUpdate(key, text, (k, v) => text);
    }
}