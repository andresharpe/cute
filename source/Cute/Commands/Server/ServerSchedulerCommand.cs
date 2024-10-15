using Cute.Commands.BaseCommands;
using Cute.Config;
using Cute.Lib.Contentful.CommandModels.ContentSyncApi;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Services;
using Microsoft.AspNetCore.Mvc;
using NCrontab;
using NCrontab.Scheduler;
using Nox.Cron;
using Spectre.Console.Cli;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace Cute.Commands.Server;

public class ServerSchedulerCommand(IConsoleWriter console, ILogger<ServerSchedulerCommand> logger, ILogger<Scheduler> cronLogger, AppSettings appSettings)
    : BaseServerCommand<ServerSchedulerCommand.Settings>(console, logger, appSettings)
{
    public class Settings : BaseServerSettings
    {
        [CommandOption("-k|--key")]
        [Description("cuteContentSyncApi key.")]
        public string Key { get; set; } = default!;
    }

    private Settings? _settings;

    private static Scheduler _scheduler = null!;

    private static object _schedulerLock = new();

    private static ConcurrentDictionary<Guid, CuteContentSyncApi> _cronTasks = [];

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        _settings = settings;

        LoadSyncApiEntries();

        DisplaySchedule();

        RefreshScheduler();

        await StartWebServer();

        return 0;
    }

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
        await context.Response.WriteAsync($"<th style='width:23%'>Info (UTC)</th>");
        await context.Response.WriteAsync($"</tr>");

        var nextRun = _scheduler.GetNextOccurrences()
            .SelectMany(i => i.ScheduledTasks, (i, j) => new { j.Id, i.NextOccurrence })
            .ToDictionary(o => o.Id, o => o.NextOccurrence);

        foreach (var (key, entry) in _cronTasks.OrderBy(kv => nextRun[kv.Key]))
        {
            string? lastRunStarted = null;
            string? lastRunFinished = null;
            string? status = null;

            if (_lastRunInfo.TryGetValue(entry.Key, out var info))
            {
                lastRunStarted = info.lastRunStarted?.ToString("R");
                lastRunFinished = info.lastRunFinished?.ToString("R");
                status = info.status;
            }

            await context.Response.WriteAsync($"<tr>");
            await context.Response.WriteAsync($"<td>{entry.Key}</td>");
            await context.Response.WriteAsync($"<td>{entry.Schedule}</td>");
            await context.Response.WriteAsync($"<td>{entry.Schedule?.ToCronExpression()}</td>");
            await context.Response.WriteAsync($"<td>");
            if (lastRunStarted is not null)
                await context.Response.WriteAsync($"<small>Last run started:</small><br><b>{lastRunStarted}</b><br>");
            if (lastRunFinished is not null)
                await context.Response.WriteAsync($"<small>Last run ended:</small><br><b>{lastRunFinished}</b><br>");
            if (status is not null)
                await context.Response.WriteAsync($"<small>Status:</small><br><b>{status}</b><br>");
            await context.Response.WriteAsync($"<small>Next run:</small><br><b>{nextRun[key].ToUniversalTime():R}</b><br>");
            await context.Response.WriteAsync($"</td>");
            await context.Response.WriteAsync($"</tr>");
        }

        await context.Response.WriteAsync($"</table>");

        await context.Response.WriteAsync($"<form action='/reload' method='POST' enctype='multipart/form-data'>");
        await context.Response.WriteAsync($"<input type='hidden' name='command' value='reload'>");
        await context.Response.WriteAsync($"<button type='submit' style='width:100%'>Reload schedule from Contentful</button>");
        await context.Response.WriteAsync($"</form>");
    }

    private void LoadSyncApiEntries()
    {
        if (_settings is null) return;

        CuteContentSyncApi? specifiedJob = null;

        if (!string.IsNullOrEmpty(_settings.Key))
        {
            specifiedJob = ContentfulConnection.GetPreviewEntryByKey<CuteContentSyncApi>(_settings.Key)
                ?? throw new CliException($"No API sync entry with key '{_settings.Key}' was found.");
        }

        var cronTasks =
            (
                specifiedJob is null
                ? ContentfulConnection.GetAllPreviewEntries<CuteContentSyncApi>().OrderBy(e => e.Order).ToArray()
                : [specifiedJob]
            )
            .Where(cronTask => !string.IsNullOrEmpty(cronTask.Schedule))
            .Where(cronTask => !cronTask.Schedule.Equals("never", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (cronTasks.Length == 0)
        {
            throw new CliException($"No data sync entries found with a valid schedule.");
        }

        _cronTasks = new(cronTasks.ToDictionary(t => Guid.NewGuid(), t => t));
    }

    private void RefreshScheduler()
    {
        if (_settings is null) return;

        var settings = _settings;

        EnsureNewScheduler(cronLogger);

        foreach (var cronTaskEntry in _cronTasks)
        {
            var cronTask = cronTaskEntry;

            var syncApiKey = cronTask.Value.Key;

            var frequency = cronTask.Value.Schedule;

            var cronSchedule = frequency.ToCronExpression().ToString();

            var scheduledTask = new AsyncScheduledTask(
                cronTask.Key,
                CrontabSchedule.Parse(cronSchedule),
                ct => Task.Run(() => ProcessAndReloadSchedule(cronTask.Value), ct)
            );

            _scheduler.AddTask(scheduledTask);
        }

        _scheduler.Start();
    }

    private static void EnsureNewScheduler(ILogger<Scheduler> cronLogger)
    {
        lock (_schedulerLock)
        {
            if (_scheduler is not null && _scheduler.IsRunning)
            {
                _scheduler.Stop();

                _scheduler.RemoveAllTasks();

                _scheduler.Dispose();

                _scheduler = null!;
            }

            _scheduler = new(cronLogger, new SchedulerOptions { DateTimeKind = DateTimeKind.Utc });
        }
    }

    private void RefreshSchedule([FromForm] string command, HttpContext context)
    {
        if (!command.Equals("reload")) return;

        LoadSyncApiEntries();

        RefreshScheduler();

        context.Response.Redirect("/");
    }

    private async Task ProcessAndReloadSchedule(CuteContentSyncApi entry)
    {
        await ProcessContentSyncApi(entry);
        LoadSyncApiEntries();
        RefreshScheduler();
        DisplaySchedule();
    }

    private void DisplaySchedule()
    {
        _console.WriteNormal("Schedule loaded at {now} ({nowFriendly})", DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("R"));

        foreach (var entry in _cronTasks.Values.OrderBy(s => s.Order))
        {
            var frequency = entry.Schedule;

            var cronSchedule = frequency?.ToCronExpression().ToString();

            _console.WriteNormal("Content sync-api '{syncApiKey}' usually scheduled to run on schedule '{frequency} ({cronSchedule})'",
                entry.Key, frequency, cronSchedule);
        }
    }

    private readonly ConcurrentDictionary<string, (DateTime? lastRunStarted, DateTime? lastRunFinished, string status)> _lastRunInfo = new();

    private async Task ProcessContentSyncApi(CuteContentSyncApi cuteContentSyncApi)
    {
        string verbosity = _settings?.Verbosity.ToString() ?? Verbosity.Normal.ToString();

        string[] args = ["content", "sync-api", "--key", cuteContentSyncApi.Key, "--verbosity", verbosity, "--apply", "--force", "--log-output"];

        _console.WriteNormal("Started content sync-api for '{syncApiKey}'", cuteContentSyncApi.Key);

        DateTime? started = DateTime.UtcNow;
        DateTime? finished = null;
        var status = "running";

        _lastRunInfo[cuteContentSyncApi.Key] = (lastRunStarted: started, lastRunFinished: finished, status);

        try
        {
            var command = new CommandAppBuilder(args).Build();

            await command.RunAsync(args);

            status = "success";
        }
        catch (Exception ex)
        {
            _console.WriteException(ex);

            status = $"error ({ex.Message})";
        }
        finally
        {
            _console.WriteNormal("Completed content sync-api for '{syncApiKey}'", cuteContentSyncApi.Key);
        }

        finished = DateTime.Now;

        _lastRunInfo[cuteContentSyncApi.Key] = (lastRunStarted: started, lastRunFinished: finished, status);
    }
}