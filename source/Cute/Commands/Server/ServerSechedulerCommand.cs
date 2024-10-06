using Cute.Commands.BaseCommands;
using Cute.Config;
using Cute.Lib.Contentful.CommandModels.ContentSyncApi;
using Cute.Lib.Exceptions;
using Cute.Services;
using Microsoft.AspNetCore.Mvc;
using NCrontab;
using NCrontab.Scheduler;
using Nox.Cron;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics;

namespace Cute.Commands.Server;

public class ServerSechedulerCommand(IConsoleWriter console, ILogger<ServerSechedulerCommand> logger, ILogger<Scheduler> cronLogger, AppSettings appSettings)
    : BaseServerCommand<ServerSechedulerCommand.Settings>(console, logger, appSettings)
{
    public class Settings : BaseServerSettings
    {
        [CommandOption("-k|--key")]
        [Description("cuteContentSyncApi key.")]
        public string Key { get; set; } = default!;
    }

    private readonly Dictionary<Guid, bool> _taskRunningStates = new();

    private Settings? _settings;

    private readonly ILogger<Scheduler> _cronLogger = cronLogger;

    private readonly Scheduler _scheduler = new Scheduler(cronLogger,
            new SchedulerOptions
            {
                DateTimeKind = DateTimeKind.Utc
            }
        );

    private Dictionary<Guid, CuteContentSyncApi> _cronTasks = [];

    public override void ConfigureWebApplication(WebApplication webApp)
    {
        webApp.MapPost("/reload", RefreshSchedule).DisableAntiforgery();
    }

    public override void ConfigureWebApplicationBuilder(WebApplicationBuilder webBuilder)
    {
        webBuilder.WebHost.ConfigureKestrel(web =>
        {
            web.ListenLocalhost(_settings!.Port);
        });
    }

    public override async Task RenderHomePageBody(HttpContext context)
    {
        await context.Response.WriteAsync($"<h4>Scheduled Tasks</h4>");

        await context.Response.WriteAsync($"<table>");
        await context.Response.WriteAsync($"<tr>");
        await context.Response.WriteAsync($"<th style='width:20%'>Task</th>");
        await context.Response.WriteAsync($"<th>Schedule</th>");
        await context.Response.WriteAsync($"<th style='width:13%'>Cron</th>");
        await context.Response.WriteAsync($"<th style='width:23%'>Next run</th>");
        await context.Response.WriteAsync($"</tr>");

        var nextRun = _scheduler.GetNextOccurrences()
            .SelectMany(i => i.ScheduledTasks, (i, j) => new { j.Id, i.NextOccurrence })
            .ToDictionary(o => o.Id, o => o.NextOccurrence);

        foreach (var (key, entry) in _cronTasks)
        {
            await context.Response.WriteAsync($"<tr>");
            await context.Response.WriteAsync($"<td>{entry.Key}</td>");
            await context.Response.WriteAsync($"<td>{entry.Schedule}</td>");
            await context.Response.WriteAsync($"<td>{entry.Schedule?.ToCronExpression()}</td>");
            await context.Response.WriteAsync($"<td>");
            await context.Response.WriteAsync($"{nextRun[key]:R}<br>");
            await context.Response.WriteAsync($"</td>");
            await context.Response.WriteAsync($"</tr>");
        }

        await context.Response.WriteAsync($"</table>");

        await context.Response.WriteAsync($"<form action='/reload' method='POST' enctype='multipart/form-data'>");
        await context.Response.WriteAsync($"<input type='hidden' name='command' value='reload'>");
        await context.Response.WriteAsync($"<button type='submit' style='width:100%'>Reload schedule from Contentful</button>");
        await context.Response.WriteAsync($"</form>");
    }

    // ...

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        _settings = settings;

        LoadSyncApiEntries();

        RefreshScheduler();

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            // Open the browser
            try
            {
                Process.Start("rundll32", $"url.dll,FileProtocolHandler http://localhost:{_settings.Port}");
            }
            catch
            {
                // Do nothing
            }
        }
        ConsoleWriter.EnableConsole = false;
        await StartWebServer();

        return 0;
    }

    private void LoadSyncApiEntries()
    {
        if (_settings is null) return;

        var cronTasks = !string.IsNullOrEmpty(_settings.Key) ? [ContentfulConnection.GetPreviewEntryByKey<CuteContentSyncApi>(_settings.Key)!]
            : ContentfulConnection.GetAllPreviewEntries<CuteContentSyncApi>().OrderBy(e => e.Order).ToArray();

        if (!cronTasks.Any())
        {
            throw new CliException($"No data sync entries found.");
        }

        _cronTasks = cronTasks.ToDictionary(t => Guid.NewGuid(), t => t);
    }

    private void RefreshScheduler()
    {
        if (_settings is null) return;

        var settings = _settings;

        _scheduler.Stop();

        _scheduler.RemoveAllTasks();

        foreach (var cronTask in _cronTasks)
        {
            var syncApiKey = cronTask.Value.Key;

            if (syncApiKey is null) continue;

            var frequency = cronTask.Value.Schedule;

            if (frequency is null) continue;

            var cronSchedule = frequency.ToCronExpression().ToString();

            _taskRunningStates[cronTask.Key] = false;

            var scheduledTask = new AsyncScheduledTask(
                cronTask.Key,
                CrontabSchedule.Parse(cronSchedule),
                ct =>
                {
                    if (_taskRunningStates[cronTask.Key])
                    {
                        return Task.CompletedTask;
                    }

                    // Set the running state to true
                    _taskRunningStates[cronTask.Key] = true;

                    try
                    {
                        return Task.Run(() => ProcessContentSyncApyAndDisplaySchedule(cronTask.Value));
                    }
                    finally
                    {
                        // Reset the running state
                        _taskRunningStates[cronTask.Key] = false;
                    }
                }
            );

            _scheduler.AddTask(scheduledTask);
        }

        _scheduler.Start();
    }

    private void RefreshSchedule([FromForm] string command, HttpContext context)
    {
        if (!command.Equals("reload")) return;

        LoadSyncApiEntries();

        RefreshScheduler();

        context.Response.Redirect("/");
    }

    private async Task ProcessContentSyncApyAndDisplaySchedule(CuteContentSyncApi entry)
    {
        await ProcessContentSyncApi(entry);

        DisplaySchedule();
    }

    private void DisplaySchedule()
    {
        foreach (var entry in _cronTasks.Values)
        {
            if (entry.Key is null) continue;

            var frequency = entry.Schedule;

            var cronSchedule = frequency?.ToCronExpression().ToString();

            _console.WriteNormal("Content sync-api '{syncApiKey}' usually scheduled to run on schedule '{frequency} ({cronSchedule})'",
                entry.Key, frequency, cronSchedule);
        }
    }

    private async Task ProcessContentSyncApi(CuteContentSyncApi cuteContentSyncApi)
    {
        string[] args = ["content", "sync-api", "-k", cuteContentSyncApi.Key, "-a", "--force"];
        var command = new CommandAppBuilder(args).Build();
        await command.RunAsync(args);
        _console.WriteBlankLine();

        _console.WriteNormal("Completed content sync-api for '{syncApiKey}'", cuteContentSyncApi.Key);
        _console.WriteBlankLine();
    }
}