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
        public string Status { get; set; } = string.Empty;
        public DateTime? LastRunFinished { get; set; }
        public DateTime? LastRunStarted { get; set; }

        public ScheduledEntry(CuteContentSyncApi cuteContentSyncApi)
        {
            Key = cuteContentSyncApi.Key;
            Schedule = cuteContentSyncApi.Schedule;
            Order = cuteContentSyncApi.Order;
            Yaml = cuteContentSyncApi.Yaml;
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

    private static Scheduler _scheduler = null!;

    private static object _schedulerLock = new();

    private static ConcurrentDictionary<Guid, ScheduledEntry> _cronTasks = [];

    private static ConcurrentDictionary<string, LinkedList<string>> _chainedStructure = [];

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
            string? lastRunStarted = entry.LastRunStarted?.ToString("R");
            string? lastRunFinished = entry.LastRunFinished?.ToString("R");
            string? status = entry.Status;

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
        var cronTasks = GetSyncApiEntries();
        _cronTasks = new(cronTasks.ToDictionary(t => Guid.NewGuid(), t => new ScheduledEntry(t)));
    }

    private void UpdateScheduler()
    {
        var syncApiEntries = GetSyncApiEntries().ToDictionary(t => t.Key, t => t);
        lock (_schedulerLock)
        {
            foreach (var (key, entry) in _cronTasks)
            {
                if (syncApiEntries.ContainsKey(entry.Key))
                {
                    var latestEntry = syncApiEntries[entry.Key];
                    if (latestEntry.Schedule != entry.Schedule)
                    {
                        var cronSchedule = latestEntry.Schedule.ToCronExpression().ToString();
                        _cronTasks[key].UpdateEntry(latestEntry);
                        _scheduler.UpdateTask(key, CrontabSchedule.Parse(cronSchedule));
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
                var cronSchedule = entry.Schedule.ToCronExpression().ToString();
                _cronTasks[key] = new ScheduledEntry(entry);
                _scheduler.AddTask(new AsyncScheduledTask(key, CrontabSchedule.Parse(cronSchedule), ct => Task.Run(() => ProcessAndUpdateSchedule(_cronTasks[key]), ct)));
            }
        }
    }

    private CuteContentSyncApi[] GetSyncApiEntries()
    {
        if (_settings is null) return [] ;

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

        var chainedKeys = cronTasks
            .Where(cronTask => cronTask.Schedule.TrimStart().StartsWith("runafter:", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(cronTask => cronTask.Key, cronTask => cronTask.Schedule.Replace("runafter:", string.Empty).Trim());

        _chainedStructure = new (GetChainedStructure(chainedKeys));

        cronTasks = cronTasks.Where(cronTask => !cronTask.Schedule.TrimStart().StartsWith("runafter:", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (cronTasks.Length == 0)
        {
            throw new CliException($"No data sync entries found with a valid schedule.");
        }

        return cronTasks;
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
                ct => Task.Run(() => ProcessAndUpdateSchedule(cronTask.Value), ct)
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

    private async Task ProcessAndUpdateSchedule(ScheduledEntry entry)
    {
        await ProcessContentSyncApi(entry);
        UpdateScheduler();
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

    private async Task ProcessContentSyncApi(ScheduledEntry cuteContentSyncApi)
    {
        string verbosity = _settings?.Verbosity.ToString() ?? Verbosity.Normal.ToString();

        string[] args = ["content", "sync-api", "--key", cuteContentSyncApi.Key, "--verbosity", verbosity, "--apply", "--force", "--log-output"];

        _console.WriteNormal("Started content sync-api for '{syncApiKey}'", cuteContentSyncApi.Key);

        DateTime? started = DateTime.UtcNow;
        DateTime? finished = null;
        var status = "running";

        cuteContentSyncApi.LastRunStarted = started;
        cuteContentSyncApi.LastRunFinished = finished;
        cuteContentSyncApi.Status = status;

        try
        {
            var command = new CommandAppBuilder(args).Build();

            await command.RunAsync(args);

            if (_chainedStructure.TryGetValue(cuteContentSyncApi.Key, out var chainedKeys))
            {
                foreach (var key in chainedKeys)
                {
                    args[3] = key;
                    command = new CommandAppBuilder(args).Build();
                    await command.RunAsync(args);
                }
            }

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

        finished = DateTime.UtcNow;

        cuteContentSyncApi.LastRunStarted = started;
        cuteContentSyncApi.LastRunFinished = finished;
        cuteContentSyncApi.Status = status;
    }

    private Dictionary<string, LinkedList<string>> GetChainedStructure(Dictionary<string, string> chainedEntries)
    {
        var result = new Dictionary<string, LinkedList<string>>();

        foreach (var (key, value) in chainedEntries)
        {
            var runAfterKey = value;

            // If the runAfterKey is not in the chainedEntries, then it is the scheduled task.
            if (!chainedEntries.ContainsKey(runAfterKey))
            {
                if (!result.ContainsKey(runAfterKey))
                {
                    result.Add(runAfterKey, new LinkedList<string>());
                }

                // add primary descendant to the start of the list.
                result[runAfterKey].AddFirst(key);
            }
            else
            {
                HashSet<string> visited = new();
                var circuit = false;
                while (chainedEntries.ContainsKey(runAfterKey))
                {
                    if(visited.Contains(runAfterKey))
                    {
                        _console.WriteAlertAccent($"Circular dependency detected in chained entries for '{key}' runafter '{value}'.");
                        circuit = true;
                        break;
                    }
                    runAfterKey = chainedEntries[value];
                    visited.Add(runAfterKey);
                }
                
                if(circuit)
                {
                    continue;
                }

                if (!result.ContainsKey(runAfterKey))
                {
                    result.Add(runAfterKey, new LinkedList<string>());
                }
                // add chained descendant to the end of the list.
                result[runAfterKey].AddLast(key);
            }
        }

        return result;
    }
}