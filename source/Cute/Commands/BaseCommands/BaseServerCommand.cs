using Cute.Config;
using Cute.Constants;
using Cute.Lib.Extensions;
using Cute.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cute.Commands.BaseCommands;

public abstract class BaseServerCommand<TSettings>(IConsoleWriter console, ILogger<BaseServerCommand<TSettings>> logger,
    AppSettings appSettings) : BaseLoggedInCommand<TSettings>(console, logger, appSettings) where TSettings : BaseServerSettings
{
    private CommandContext? _commandContext;

    public override ValidationResult Validate(CommandContext context, TSettings settings)
    {
        if (settings.Port == 0)
        {
            return ValidationResult.Error($"The port for the webserver (--port) must be specified.");
        }

        return base.Validate(context, settings);
    }

    private string HtmlHeader => $"""
    <!DOCTYPE html>
    <html lang="en">
        <head>
        <meta charset="utf-8">
        <link rel="icon" type="image/x-icon" href="https://raw.githubusercontent.com/andresharpe/cute/master/docs/images/cute.png">
        <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css">
        <title>{_commandContext?.Name}</title>
        <link rel="stylesheet" href="https://cdn.simplecss.org/simple-v1.css">
        <script src="https://cdn.jsdelivr.net/gh/google/code-prettify@master/loader/run_prettify.js"></script>
        {_prettifyColors}
        </head>
        <body>
    """;

    private static string HtmlFooter => $"""
        <footer><a href="{Globals.AppMoreInfo}"><i style="font-size:20px" class="fa">&#xf09b;</i>&nbsp;&nbsp;Source code on GitHub</a></footer>
        </body>
    </html>
    """;

    private const string _prettifyColors = """
    <style>
    .atv,.str{color:#ec7600}.kwd{color:#93c763}.com{color:#66747b}.typ{color:#678cb1}.lit{color:#facd22}.pln,.pun{color:#f1f2f3}.tag{color:#8ac763}
    .atn{color:#e0e2e4}.dec{color:{_highlighted}}pre.prettyprint{border:0 solid #888}ol.linenums{margin-top:0;margin-bottom:0}.prettyprint{background:#000}
    li.L0,li.L1,li.L2,li.L3,li.L4,li.L5,li.L6,li.L7,li.L8,li.L9{color:#555;list-style-type:decimal}
    li.L1,li.L3,li.L5,li.L7,li.L9{background:#111}@media print{.kwd,.tag,.typ{font-weight:700}.atv,.str{color:#060}.kwd,.tag{color:#006}
    .com{color:#600;font-style:italic}.typ{color:#404}.lit{color:#044}.pun{color:#440}.pln{color:#000}.atn{color:#404}}
    .center{display:block;margin-left:auto;margin-right:auto;width:50%}
    </style>
    """;

    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken)
    {
        _commandContext = context;

        return await base.ExecuteAsync(context, settings, cancellationToken);
    }

    public abstract void ConfigureWebApplication(WebApplication webApp);

    public abstract Task RenderHomePageBody(HttpContext context);

    public async Task StartWebServer(TSettings settings)
    {
        var webBuilder = WebApplication.CreateBuilder();

        webBuilder.Services.AddHealthChecks();

        webBuilder.Logging.ClearProviders().AddSerilog();

        ConfigureWebApplicationBuilder(webBuilder, settings);

        var webApp = webBuilder.Build();

        webApp.MapGet("/", DisplayHomePage);

        ConfigureWebApplication(webApp);

        webApp.MapHealthChecks("/healthz");

        _console.WriteNormal("Ready...");

        ConsoleWriter.EnableConsole = false;

        try
        {
            await webApp.RunAsync();
        }
        catch (Exception ex)
        {
            _console.WriteException(ex);
        }
    }

    public virtual void ConfigureWebApplicationBuilder(WebApplicationBuilder webBuilder, TSettings settings)
    {
        webBuilder.WebHost.ConfigureKestrel(web =>
        {
            web.ListenAnyIP(settings.Port);
        });
    }

    private async Task DisplayHomePage(HttpContext context, [FromServices] HealthCheckService healthCheckService)
    {
        context.Response.Headers.TryAdd("Content-Type", "text/html");

        var health = await healthCheckService.CheckHealthAsync();

        var statusDot = health.Status switch
        {
            HealthStatus.Unhealthy => "\U0001f534",
            HealthStatus.Degraded => "\U0001f7e1",
            HealthStatus.Healthy => "\U0001f7e2",
            _ => throw new NotImplementedException(),
        };

        await context.Response.WriteAsync(HtmlHeader);

        await context.Response.WriteAsync($"""<img src="https://raw.github.com/andresharpe/cute/master/docs/images/cute-logo.png" class="center">""");

        await context.Response.WriteAsync($"<h3>{Globals.AppLongName}</h3>");

        await context.Response.WriteAsync($"{statusDot} {health.Status}");

        await context.Response.WriteAsync($"<p>{Globals.AppDescription}</p>");

        var defaultSpace = await ContentfulConnection.GetDefaultSpaceAsync();
        var contentfulUser = await ContentfulConnection.GetCurrentUserAsync();
        var defaultEnvironment = await ContentfulConnection.GetDefaultEnvironmentAsync();

        await context.Response.WriteAsync($"""
        Logged into Contentful space <pre>{defaultSpace.Name} ({defaultSpace.Id()})</pre>
        as user <pre>{contentfulUser.Email} (id: {contentfulUser.SystemProperties.Id})</pre>
        using environment <pre>{defaultEnvironment.Id()}</pre>
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

        await RenderHomePageBody(context);

        await context.Response.WriteAsync(HtmlFooter);

        return;
    }
}