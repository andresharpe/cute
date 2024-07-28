using Cute.Constants;
using Cute.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands;

public class WebserverCommand : LoggedInCommand<WebserverCommand.Settings>
{
    private static readonly JsonSerializerSettings _jsonSettings = new() { ContractResolver = new CamelCasePropertyNamesContractResolver() };

    private readonly ICommandApp _commandApp;

    private readonly ILogger<WebserverCommand> _logger;

    public WebserverCommand(IConsoleWriter console, IPersistedTokenCache tokenCache,
        ICommandApp commandApp, ILogger<WebserverCommand> logger)
            : base(console, tokenCache, logger)
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

        var webBuilder = WebApplication.CreateBuilder();

        webBuilder.Services.AddHealthChecks();

        webBuilder.Logging.ClearProviders().AddSerilog();

        webBuilder.WebHost.ConfigureKestrel(web =>
        {
            web.ListenLocalhost(settings.Port);
        });

        var webapp = webBuilder.Build();

        webapp.MapGet("/", DisplayHomePage);

        webapp.MapPost("/", ProcessWebhook);

        webapp.MapHealthChecks("/healthz");

        await webapp.RunAsync();

        return 0;
    }

    private async Task DisplayHomePage(HttpContext context, [FromServices] HealthCheckService healthCheckService)
    {
        context.Response.Headers.TryAdd("Content-Type", "text/html");

        var htmlStart = $"""
            <!DOCTYPE html>
            <html lang="en">
              <head>
                <meta charset="utf-8">
                <link rel="icon" type="image/x-icon" href="https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute.png">
                <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css">
                <title>{Globals.AppLongName}</title>
                <link rel="stylesheet" href="https://cdn.simplecss.org/simple-v1.css">
                <script src="https://cdn.jsdelivr.net/gh/google/code-prettify@master/loader/run_prettify.js"></script>
                {_prettifyColors}
              </head>
              <body>
            """;

        var health = await healthCheckService.CheckHealthAsync();

        var statusDot = health.Status switch
        {
            HealthStatus.Unhealthy => "\U0001f534",
            HealthStatus.Degraded => "\U0001f7e1",
            HealthStatus.Healthy => "\U0001f7e2",
            _ => throw new NotImplementedException(),
        };

        await context.Response.WriteAsync(htmlStart);

        await context.Response.WriteAsync($"""<img src="https://raw.github.com/andresharpe/cute/master/docs/images/cute-logo.png" class="center">""");

        await context.Response.WriteAsync($"<h3>{Globals.AppLongName}</h3>");

        await context.Response.WriteAsync($"{statusDot} {health.Status}");

        await context.Response.WriteAsync($"<p>{Globals.AppDescription}</p>");

        await context.Response.WriteAsync($"""
            Logged into Contentful space <pre>{ContentfulSpace.Name} ({ContentfulSpaceId})</pre>
            as user <pre>{ContentfulUser.Email} (id: {ContentfulUser.SystemProperties.Id})</pre>
            using environment <pre>{ContentfulEnvironmentId}</pre>
            """);

        await context.Response.WriteAsync($"<h4>App Version</h4>");

        await context.Response.WriteAsync($"{Globals.AppVersion}<br>");

        if (health.Entries.Count > 0)
        {
            await context.Response.WriteAsync($"<h4>Webserver Health Report</h4>");

            await context.Response.WriteAsync($"<table>");
            await context.Response.WriteAsync($"<tr>");
            await context.Response.WriteAsync($"<th>Key</th>");
            await context.Response.WriteAsync($"<th>Status</th>");
            await context.Response.WriteAsync($"<th>Description</th>");
            await context.Response.WriteAsync($"<th>Data</th>");
            await context.Response.WriteAsync($"</tr>");

            foreach (var entry in health.Entries)
            {
                await context.Response.WriteAsync($"<tr>");

                await context.Response.WriteAsync($"<td>{entry.Key}</td>");
                await context.Response.WriteAsync($"<td>{entry.Value.Status}</td>");
                await context.Response.WriteAsync($"<td>{entry.Value.Description}</td>");

                await context.Response.WriteAsync($"<td>");
                foreach (var item in entry.Value.Data)
                {
                    await context.Response.WriteAsync($"<b>{item.Key}</b>: {item.Value}<br>");
                }
                await context.Response.WriteAsync($"</td>");

                await context.Response.WriteAsync($"</tr>");
            }

            await context.Response.WriteAsync($"</table>");
        }

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

        var htmlEnd = $"""
                <footer><a href="{Globals.AppMoreInfo}"><i style="font-size:20px" class="fa">&#xf09b;</i>&nbsp;&nbsp;Source code on GitHub</a></footer>
              </body>
            </html>
            """;

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
        var bulkActionId = request.Headers["X-Contentful-Bulk-Action-Id"];

        using (_logger.BeginScope("{actionId}", bulkActionId))
        {
            _logger.LogInformation("Task started: {headers}", request.Headers);

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
                _logger.LogError(ex, "Task error!");
            }

            _logger.LogInformation("Task completed.");
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

    private const string _prettifyColors = """
        <style>
            .str
            {
                color: #EC7600;
            }
            .kwd
            {
                color: #93C763;
            }
            .com
            {
                color: #66747B;
            }
            .typ
            {
                color: #678CB1;
            }
            .lit
            {
                color: #FACD22;
            }
            .pun
            {
                color: #F1F2F3;
            }
            .pln
            {
                color: #F1F2F3;
            }
            .tag
            {
                color: #8AC763;
            }
            .atn
            {
                color: #E0E2E4;
            }
            .atv
            {
                color: #EC7600;
            }
            .dec
            {
                color: purple;
            }
            pre.prettyprint
            {
                border: 0px solid #888;
            }
            ol.linenums
            {
                margin-top: 0;
                margin-bottom: 0;
            }
            .prettyprint {
                background: #000;
            }
            li.L0, li.L1, li.L2, li.L3, li.L4, li.L5, li.L6, li.L7, li.L8, li.L9
            {
                color: #555;
                list-style-type: decimal;
            }
            li.L1, li.L3, li.L5, li.L7, li.L9 {
                background: #111;
            }
            @media print
            {
                .str
                {
                    color: #060;
                }
                .kwd
                {
                    color: #006;
                    font-weight: bold;
                }
                .com
                {
                    color: #600;
                    font-style: italic;
                }
                .typ
                {
                    color: #404;
                    font-weight: bold;
                }
                .lit
                {
                    color: #044;
                }
                .pun
                {
                    color: #440;
                }
                .pln
                {
                    color: #000;
                }
                .tag
                {
                    color: #006;
                    font-weight: bold;
                }
                .atn
                {
                    color: #404;
                }
                .atv
                {
                    color: #060;
                }
            }
            .center {
                display: block;
                margin-left: auto;
                margin-right: auto;
                width: 50%;
            }        </style>
        """;

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