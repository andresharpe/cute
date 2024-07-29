using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands;

public sealed class WebserverCommand : WebCommand<WebserverCommand.Settings>
{
    private static readonly JsonSerializerSettings _jsonSettings = new() { ContractResolver = new CamelCasePropertyNamesContractResolver() };

    private readonly ICommandApp _commandApp;

    private readonly ILogger<WebserverCommand> _logger;

    public WebserverCommand(IConsoleWriter console, ILogger<WebserverCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings, ICommandApp commandApp)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _commandApp = commandApp;

        _logger = logger;
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

        await StartWebServer();

        return 0;
    }

    public override void ConfigureWebApplicationBuilder(WebApplicationBuilder webBuilder, Settings settings)
    {
        webBuilder.WebHost.ConfigureKestrel(web =>
        {
            web.ListenLocalhost(settings.Port);
        });
    }

    public override void ConfigureWebApplication(WebApplication webApp, Settings settings)
    {
        webApp.MapPost("/", ProcessWebhook);
    }

    public override async Task RenderHomePageBody(HttpContext context)
    {
        await context.Response.WriteAsync($"<h4>Available Commands</h4>");

        await context.Response.WriteAsync($"<ul>");

        foreach (var commandName in Enum.GetNames(typeof(ValidCommand)))
        {
            await context.Response.WriteAsync($"<li>{commandName}</li>");
        }

        await context.Response.WriteAsync($"</ul>");

        await context.Response.WriteAsync($"<h4>Example Contentful Payload</h4>");

        await context.Response.WriteAsync("""
            Setup your Contentful webhook payload as follows.
            """);

        await context.Response.WriteAsync($"""<pre class="prettyprint">{_payLoadFormatExample}</pre>""");
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

        var webhookCommandCollection = JsonConvert.DeserializeObject<WebhookCommandRequest>(body, _jsonSettings);

        if (webhookCommandCollection is null)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("{{ \"Response\": \"Invalid Entry\" }}");
            return;
        }

        var request = new WebhookRequest { Headers = headers, CommandRequests = webhookCommandCollection };

        _ = Task.Run(async () => await ExecuteCommand(request));

        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("{{ \"Response\": \"Ok\" }}");

        return;
    }

    private async Task ExecuteCommand(WebhookRequest request)
    {
        if (!request.Headers.TryGetValue("X-Contentful-Bulk-Action-Id", out string? bulkActionId) || bulkActionId is null)
        {
            bulkActionId = Guid.NewGuid().ToString();
        }

        using (_logger.BeginScope("{actionId}", bulkActionId))
        {
            _console.WriteBlankLine();
            _console.WriteAlertAccent("Task started..");
            _console.WriteBlankLine();

            _logger.LogInformation("Headers: {headers}", request.Headers);

            try
            {
                foreach (var command in request.CommandRequests.Commands)
                {
                    var args = command.Parameters.SelectMany(p => new string[] { p.Key, p.Value }).ToList();

                    args.Insert(0, command.Command.ToString());

                    await _commandApp.RunAsync(args.ToArray());
                }
            }
            catch (Exception ex)
            {
                _console.WriteBlankLine();
                _console.WriteException(ex);
            }

            _console.WriteBlankLine();
            _console.WriteAlertAccent("Task completed.");
            _console.WriteBlankLine();
        }

        return;
    }

    private readonly struct WebhookRequest
    {
        public readonly IReadOnlyDictionary<string, string> Headers { get; init; }
        public readonly WebhookCommandRequest CommandRequests { get; init; }
    }

    private class WebhookCommandRequest
    {
        public string SpaceId { get; set; } = default!;
        public string EnvironmentId { get; set; } = default!;
        public string ContentTypeId { get; set; } = default!;
        public string EntryId { get; set; } = default!;
        public string Version { get; set; } = default!;
        public WebhookCommand[] Commands { get; set; } = default!;
    }

    private class WebhookCommand
    {
        public ValidCommand Command { get; set; } = default!;
        public Dictionary<string, string> Parameters { get; set; } = default!;
    }

    private enum ValidCommand
    {
        join,
        generate,
    }

    private const string _payLoadFormatExample = """
            {
              "spaceId": "{ /payload/sys/space/sys/id }",
              "environmentId": "{ /payload/sys/environment/sys/id }",
              "contentTypeId": "{ /payload/sys/contentType/sys/id }",
              "entryId": "{ /payload/sys/id }",
              "version": "{ /payload/sys/version }",
              "commands": [
                {
                  "command": "join",
                  "parameters": {
                    "--join-id": "ContentGeo",
                    "--entry-id": "{ /payload/sys/id }"
                  }
                },
                {
                  "command": "generate",
                  "parameters": {
                    "--prompt-id": "DataGeo.BusinessRationale",
                    "--entry-id": "{ /payload/sys/id }",
                    "--delay": 0
                  }
                },
                {
                  "command": "generate",
                  "parameters": {
                    "--prompt-id": "ContentGeo.BusinessRationale",
                    "--related-entry-id": "{ /payload/sys/id }",
                    "--delay": 0
                  }
                }
              ]
            }
            """;
}