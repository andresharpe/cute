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

    private class ScheduledEntry : CuteContentSyncApi
    {
        public string? Status { get; set; }
        public DateTime? LastRunFinished { get; set; }
        public DateTime? LastRunStarted { get; set; }
        public TimeSpan? Duration => LastRunFinished - LastRunStarted;

        public string? CronSchedule { get; set; }

        public ScheduledEntry? RunNext = null;

        public bool IsRunAfter => Schedule.StartsWith("runafter:", StringComparison.OrdinalIgnoreCase);

        public ScheduledEntry(CuteContentSyncApi cuteContentSyncApi)
        {
            UpdateEntry(cuteContentSyncApi);
        }

        public void UpdateEntry(CuteContentSyncApi cuteContentSyncApi)
        {
            Key = cuteContentSyncApi.Key;
            Schedule = cuteContentSyncApi.Schedule;
            Order = cuteContentSyncApi.Order;
            Yaml = cuteContentSyncApi.Yaml;
        }
    }

    private Settings? _settings;

    private readonly ILogger<Scheduler> _cronLogger = cronLogger;

    private static Scheduler _scheduler = null!;

    private static readonly object _schedulerLock = new();

    private static readonly ConcurrentDictionary<Guid, ScheduledEntry> _cronTasks = [];

    private static ScheduledEntry GetScheduleByKey(string key) =>
        _cronTasks.Values.First(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        _settings = settings;

        EnsureNewScheduler(_cronLogger);

        UpdateScheduler();

        _scheduler.Start();

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

        var nextRuns = _scheduler.GetNextOccurrences()
            .SelectMany(i => i.ScheduledTasks, (i, j) => new { j.Id, i.NextOccurrence })
            .ToDictionary(o => o.Id, o => o.NextOccurrence);

        foreach (var (key, entry) in _cronTasks.OrderBy(kv => nextRuns[kv.Key]))
        {
            string? lastRunStarted = entry.LastRunStarted?.ToString("R");
            string? lastRunFinished = entry.LastRunFinished?.ToString("R");
            string? status = entry.Status;
            string? nextRun = nextRuns[key].ToString("R");

            string schedule = entry.Schedule;

            await context.Response.WriteAsync($"<tr>");
            await context.Response.WriteAsync($"<td>{entry.Key}</td>");
            await context.Response.WriteAsync($"<td>{entry.Schedule}</td>");
            await context.Response.WriteAsync($"<td>{entry.CronSchedule}</td>");
            await context.Response.WriteAsync($"<td>");
            if (lastRunStarted is not null)
                await context.Response.WriteAsync($"<small>Last run started:</small><br><b>{lastRunStarted}</b><br>");
            if (lastRunFinished is not null)
                await context.Response.WriteAsync($"<small>Last run ended:</small><br><b>{lastRunFinished}</b><br>");
            if (status is not null)
                await context.Response.WriteAsync($"<small>Status:</small><br><b>{status}</b><br>");
            await context.Response.WriteAsync($"<small>Next run:</small><br><b>{nextRun}</b><br>");
            await context.Response.WriteAsync($"</td>");
            await context.Response.WriteAsync($"</tr>");
        }

        await context.Response.WriteAsync($"</table>");

        await context.Response.WriteAsync($"<form action='/reload' method='POST' enctype='multipart/form-data'>");
        await context.Response.WriteAsync($"<input type='hidden' name='command' value='reload'>");
        await context.Response.WriteAsync($"<button type='submit' style='width:100%'>Reload schedule from Contentful</button>");
        await context.Response.WriteAsync($"</form>");
    }

    private static void UpdateNextRunLinks()
    {
        foreach (var entry in _cronTasks.Values)
        {
            entry.RunNext = null;
        }

        foreach (var entry in _cronTasks.Values)
        {
            if (entry.IsRunAfter)
            {
                var targetKey = entry.Schedule.Replace("runafter:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                var targetEntry = GetScheduleByKey(targetKey);
                targetEntry.RunNext = entry;
            }
        }
    }

    private void UpdateScheduler()
    {
        // Scheduled
        var syncApiEntries = GetSyncApiEntries()
            .ToDictionary(t => t.Key);

        lock (_schedulerLock)
        {
            foreach (var (key, entry) in _cronTasks)
            {
                if (syncApiEntries.TryGetValue(entry.Key, out CuteContentSyncApi? latestEntry))
                {
                    var isTimeScheduled = !latestEntry.Schedule.StartsWith("runafter:", StringComparison.OrdinalIgnoreCase);

                    if (!latestEntry.Schedule.Equals(entry.Schedule, StringComparison.OrdinalIgnoreCase))
                    {
                        if (isTimeScheduled)
                        {
                            var cronSchedule = latestEntry.Schedule.ToCronExpression().ToString();
                            _cronTasks[key].UpdateEntry(latestEntry);
                            _cronTasks[key].CronSchedule = cronSchedule;
                            _scheduler.UpdateTask(key, CrontabSchedule.Parse(cronSchedule));
                        }
                        else
                        {
                            _cronTasks[key].UpdateEntry(latestEntry);
                            _cronTasks[key].CronSchedule = latestEntry.Schedule;
                        }
                    }
                    syncApiEntries.Remove(entry.Key);
                }
                else
                {
                    _cronTasks.Remove(key, out _);
                    _scheduler.RemoveTask(key);
                }
            }

            foreach (var entry in syncApiEntries.Values)
            {
                var key = Guid.NewGuid();
                var isTimeScheduled = !entry.Schedule.StartsWith("runafter:", StringComparison.OrdinalIgnoreCase);

                if (isTimeScheduled)
                {
                    var cronSchedule = entry.Schedule.ToCronExpression().ToString();
                    _cronTasks[key] = new ScheduledEntry(entry)
                    {
                        CronSchedule = cronSchedule
                    };
                    _scheduler.AddTask(new AsyncScheduledTask(key, CrontabSchedule.Parse(cronSchedule), ct => Task.Run(() => ProcessAndUpdateSchedule(_cronTasks[key]), ct)));
                }
                else
                {
                    _cronTasks[key] = new ScheduledEntry(entry)
                    {
                        CronSchedule = entry.Schedule
                    };
                }
            }
        }

        UpdateNextRunLinks();

        DisplaySchedule();
    }

    private CuteContentSyncApi[] GetSyncApiEntries()
    {
        if (_settings is null) throw new CliException("Settings can't be null");

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

        return cronTasks;
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

        EnsureNewScheduler(_cronLogger);

        DisplaySchedule();

        _scheduler.Start();

        context.Response.Redirect("/");
    }

    private async Task ProcessAndUpdateSchedule(ScheduledEntry entry)
    {
        await ProcessContentSyncApi(entry);

        DisplaySchedule();
    }

    private void DisplaySchedule()
    {
        _console.WriteNormal("Schedule loaded at {now} ({nowFriendly})", DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("R"));

        foreach (var entry in _cronTasks.Values.OrderBy(s => s.Order))
        {
            var frequency = entry.Schedule;

            var cronSchedule = entry.CronSchedule;

            _console.WriteNormal("Content sync-api '{syncApiKey}' usually scheduled to run on schedule '{frequency} ({cronSchedule})'",
                entry.Key, frequency, cronSchedule);
        }
    }

    private async Task ProcessContentSyncApi(ScheduledEntry cuteContentSyncApi)
    {
        string verbosity = _settings?.Verbosity.ToString() ?? Verbosity.Normal.ToString();

        var entry = cuteContentSyncApi;

        while (entry is not null)
        {
            string[] args = ["content", "sync-api", "--key", entry.Key, "--verbosity", verbosity, "--apply", "--force", "--log-output"];

            _console.WriteNormal("Started content sync-api for '{syncApiKey}'", entry.Key);

            entry.LastRunStarted = DateTime.UtcNow;
            entry.LastRunFinished = null;
            entry.Status = "running";

            try
            {
                var command = new CommandAppBuilder(args).Build();

                await command.RunAsync(args);

                entry.Status = "success";
                entry.LastRunFinished = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _console.WriteException(ex);
                entry.Status = $"error ({ex.Message})";
                entry.LastRunFinished = DateTime.UtcNow;
            }
            finally
            {
                _console.WriteNormal("Completed content sync-api for '{syncApiKey}'", entry.Key);
            }

            entry = entry.RunNext;
        }
    }
}