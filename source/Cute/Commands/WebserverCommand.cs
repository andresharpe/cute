using Contentful.Core.Models;
using Cute.Constants;
using Cute.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Cute.Commands;

public class WebserverCommand : LoggedInCommand<WebserverCommand.Settings>
{
    private readonly TypeInfo[] _commands;

    private static readonly JsonSerializerSettings _jsonSettings = new() { ContractResolver = new CamelCasePropertyNamesContractResolver() };

    private readonly ConcurrentQueue<Task> _requests = [];

    private readonly ICommandApp _commandApp;

    public WebserverCommand(IConsoleWriter console, IPersistedTokenCache tokenCache, ICommandApp commandApp)
            : base(console, tokenCache)
    {
        _commands = typeof(WebserverCommand).Assembly.DefinedTypes
            .Where(x => x.IsAssignableTo(typeof(ICommand)) && !x.IsAbstract)
            .ToArray();

        _commandApp = commandApp;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-p|--port")]
        [Description("The port to listen on")]
        public int Port { get; set; } = 8083;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        if (result != 0) return result;

        var webBuilder = WebApplication.CreateBuilder();

        webBuilder.WebHost.ConfigureKestrel(web =>
        {
            web.ListenLocalhost(settings.Port);
        });

        var webapp = webBuilder.Build();

        webapp.MapGet("/", DisplayHomePage);

        webapp.MapPost("/", ProcessWebhook);

        webapp.Run();

        return 0;
    }

    private async Task DisplayHomePage(HttpContext context)
    {
        context.Response.Headers.TryAdd("Content-Type", "text/html");

        var htmlStart = $"""
            <!DOCTYPE html>
            <html lang="en">
              <head>
                <meta charset="utf-8">
                <title>{Globals.AppLongName}</title>
                <link rel="stylesheet" href="https://cdn.simplecss.org/simple-v1.css">
              </head>
              <body>
            """;

        var htmlEnd = $"""
              </body>
            </html>
            """;

        await context.Response.WriteAsync(htmlStart);

        await context.Response.WriteAsync($"<h1>Requests</h1>");

        foreach (var request in _requests)
        {
            await context.Response.WriteAsync($"<b>{request.Id} {request.IsCompleted}</b><br>");
        }

        await context.Response.WriteAsync($"<h1>Commands</h1>");

        foreach (var info in _commands)
        {
            await context.Response.WriteAsync($"<b>{info.Name}</b><br>");
        }

        await context.Response.WriteAsync($"<h1>Version</h1>");

        await context.Response.WriteAsync($"<b>{VersionChecker.GetInstalledCliVersion()}</b><br>");

        await context.Response.WriteAsync(htmlEnd);

        return;
    }

    private async Task ProcessWebhook(HttpContext context)
    {
        context.Response.Headers.TryAdd("Content-Type", "application/json");

        if (!context.Request.HasJsonContentType())
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("{{ \"Response\": \"NotJson\" }}");
            return;
        }

        var headers = context.Request.Headers
            .Where(h => h.Key.StartsWith("X-Contentful-"))
            .ToDictionary(h => h.Key, h => h.Value.First() ?? string.Empty);

        if (headers is null || headers.Count == 0)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("{{ \"Response\": \"Access Denied\" }}");
            return;
        }

        using var reader = new StreamReader(context.Request.Body);

        var body = await reader.ReadToEndAsync();

        var webhookCommandCollection = JsonConvert.DeserializeObject<WebhookCommandCollection>(body, _jsonSettings);

        if (webhookCommandCollection is null)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("{{ \"Response\": \"Invalid Entry\" }}");
            return;
        }

        var request = new WebhookRequest { Headers = headers, CommandCollection = webhookCommandCollection };

        _requests.Enqueue(Task.Run(async () => await ExecuteCommand(request)));

        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("{{ \"Response\": \"Ok\" }}");

        return;
    }

    private async Task ExecuteCommand(WebhookRequest request)
    {
        foreach (var command in request.CommandCollection.Commands)
        {
            var args = command.Parameters.SelectMany(p => new string[] { p.Key, p.Value }).ToList();

            args.Insert(0, command.Command.ToString());

            await _commandApp.RunAsync(args.ToArray());
        }

        return;
    }

    private readonly struct WebhookRequest
    {
        public readonly IReadOnlyDictionary<string, string> Headers { get; init; }
        public readonly WebhookCommandCollection CommandCollection { get; init; }
    }

    public class WebhookCommandCollection
    {
        public string SpaceId { get; set; } = default!;
        public string EnvironmentId { get; set; } = default!;
        public string ContentTypeId { get; set; } = default!;
        public string EntityId { get; set; } = default!;
        public string Version { get; set; } = default!;
        public WebhookCommand[] Commands { get; set; } = default!;
    }

    public class WebhookCommand
    {
        public ValidCommand Command { get; set; } = default!;
        public Dictionary<string, string> Parameters { get; set; } = default!;
    }

    public enum ValidCommand
    {
        join,
        generate,
    }
}