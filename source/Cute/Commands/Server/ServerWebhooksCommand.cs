using Contentful.Core.Models.Management;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Spectre.Console.Cli;

namespace Cute.Commands.Server;

public class ServerWebhooksCommand(IConsoleWriter console, ILogger<ServerWebhooksCommand> logger, AppSettings appSettings, ICommandApp commandApp)
    : BaseServerCommand<BaseServerSettings>(console, logger, appSettings)
{
    private readonly ICommandApp _commandApp = commandApp;

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
    private static readonly JsonSerializerSettings _jsonSettings = new() { ContractResolver = new CamelCasePropertyNamesContractResolver() };
    private readonly HashSet<string> _validCommands = new()
    {
        "content join",
        "content generate"
    };

    public override void ConfigureWebApplication(WebApplication webApp)
    {
        webApp.MapPost("/", ProcessWebhook);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, BaseServerSettings settings)
    {
        await StartWebServer(settings);

        return 0;
    }

    public override async Task RenderHomePageBody(HttpContext context)
    {
        await context.Response.WriteAsync($"<h4>Available Commands</h4>");

        await context.Response.WriteAsync($"<ul>");

        foreach (var commandName in _validCommands)
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
        _console.WriteNormal(JsonConvert.SerializeObject(request));

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
                    var args = command.Command
                        .Trim()
                        .Split(' ')
                        .Union(command.Parameters.SelectMany(p => new string[] { p.Key, p.Value }).ToList());

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
        public string Command { get; set; } = default!;
        public Dictionary<string, string> Parameters { get; set; } = default!;
    }
}