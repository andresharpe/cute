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

    private static ConcurrentDictionary<string, LinkedList<ScheduledEntry>> _chainedStructure = [];

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

        var renderRow = async (HttpContext context, string key, string schedule, string? cronExpression, string? lastRunStartedStr, string? lastRunFinishedStr, string? status, string nextRunStr) =>
        {
            await context.Response.WriteAsync($"<tr>");
            await context.Response.WriteAsync($"<td>{key}</td>");
            await context.Response.WriteAsync($"<td>{schedule}</td>");
            await context.Response.WriteAsync($"<td>{cronExpression}</td>");
            await context.Response.WriteAsync($"<td>");
            if (lastRunStartedStr is not null)
                await context.Response.WriteAsync($"<small>Last run started:</small><br><b>{lastRunStartedStr}</b><br>");
            if (lastRunFinishedStr is not null)
                await context.Response.WriteAsync($"<small>Last run ended:</small><br><b>{lastRunFinishedStr}</b><br>");
            if (status is not null)
                await context.Response.WriteAsync($"<small>Status:</small><br><b>{status}</b><br>");
            await context.Response.WriteAsync($"<small>Next run:</small><br><b>{nextRunStr}</b><br>");
            await context.Response.WriteAsync($"</td>");
            await context.Response.WriteAsync($"</tr>");
        };

        foreach (var (key, entry) in _cronTasks.OrderBy(kv => nextRun[kv.Key]))
        {
            string? lastRunStarted = entry.LastRunStarted?.ToString("R");
            string? lastRunFinished = entry.LastRunFinished?.ToString("R");
            string? status = entry.Status;

            string schedule = entry.Schedule;

            await renderRow(context, entry.Key, schedule, entry.Schedule?.ToCronExpression().ToString(), lastRunStarted, lastRunFinished, status, nextRun[key].ToUniversalTime().ToString("R"));

            if (_chainedStructure.TryGetValue(entry.Key, out var chainedEntries))
            {
                var accumulatedDuration = entry.Duration ?? new TimeSpan(0);
                foreach (var scheduledEntry in chainedEntries)
                {
                    await renderRow(context, scheduledEntry.Key, scheduledEntry.Schedule, null, scheduledEntry.LastRunStarted?.ToString("R"), scheduledEntry.LastRunFinished?.ToString("R"), scheduledEntry.Status, (nextRun[key] + accumulatedDuration).ToUniversalTime().ToString("R"));
                    accumulatedDuration += scheduledEntry.Duration ?? new TimeSpan(0);
                }
            }
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

        var chainedSyncApiEntries = cronTasks
            .Where(cronTask => cronTask.Schedule.TrimStart().StartsWith("runafter:", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(cronTask => cronTask.Key, cronTask => cronTask);

        cronTasks = cronTasks.Where(cronTask => !cronTask.Schedule.TrimStart().StartsWith("runafter:", StringComparison.OrdinalIgnoreCase)).ToArray();

        var masterSyncApiEntries = cronTasks.ToDictionary(cronTask => cronTask.Key, cronTask => cronTask);
        UpdateChainedStructure(chainedSyncApiEntries, masterSyncApiEntries);


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

            cuteContentSyncApi.LastRunStarted = started;
            await command.RunAsync(args);

            status = "success";

            finished = DateTime.UtcNow;
            cuteContentSyncApi.LastRunFinished = finished;
            cuteContentSyncApi.Status = status;

            if (_chainedStructure.TryGetValue(cuteContentSyncApi.Key, out var chainedEntries))
            {
                foreach (var chainedEntry in chainedEntries)
                {
                    args[3] = chainedEntry.Key;
                    try
                    {

                        _console.WriteNormal("Started chained content sync-api for '{syncApiKey}'", chainedEntry.Key);
                        started = DateTime.UtcNow;
                        status = "running";

                        chainedEntry.LastRunStarted = started;
                        chainedEntry.Status = status;

                        command = new CommandAppBuilder(args).Build();
                        await command.RunAsync(args);

                        status = "success";

                        finished = DateTime.UtcNow;

                        chainedEntry.LastRunFinished = finished;
                        chainedEntry.Status = status;
                    }
                    catch (Exception ex)
                    {
                        status = $"error ({ex.Message})";
                        finished = DateTime.UtcNow;

                        chainedEntry.LastRunFinished = finished;
                        chainedEntry.Status = status;
                    }
                    finally
                    {
                        _console.WriteNormal("Completed chained content sync-api for '{syncApiKey}'", chainedEntry.Key);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _console.WriteException(ex);

            status = $"error ({ex.Message})";
            finished = DateTime.UtcNow;

            cuteContentSyncApi.LastRunFinished = finished;
            cuteContentSyncApi.Status = status;
        }
        finally
        {
            _console.WriteNormal("Completed content sync-api for '{syncApiKey}'", cuteContentSyncApi.Key);
        }
    }

    // This is an immutable structure to track schedule information for chained entries.
    private Dictionary<string, ScheduledEntry> chainedEntryTracker = [];
    private void UpdateChainedStructure(Dictionary<string, CuteContentSyncApi> chainedSyncApiEntries, Dictionary<string, CuteContentSyncApi> masterSyncApiEntries)
    {
        var result = new Dictionary<string, LinkedList<ScheduledEntry>>();

        foreach (var (key, value) in chainedSyncApiEntries)
        {
            var runAfterKey = value.Schedule.Replace("runafter:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            if (chainedEntryTracker.ContainsKey(key))
            {
                // If schedule is changed we want to reset schedule information, otherwise update the entry.
                if (chainedEntryTracker[key].Schedule != value.Schedule)
                {
                    _console.WriteNormal($"Schedule change detected for cuteContentSyncApi chained entry with key: '{key}' from '{chainedEntryTracker[key].Schedule}' to '{value.Schedule}'.");
                    chainedEntryTracker[key] = new ScheduledEntry(value);
                }
                else 
                {
                    chainedEntryTracker[key].UpdateEntry(value);
                }
            }
            else
            {
                chainedEntryTracker.Add(key, new ScheduledEntry(value));
                _console.WriteNormal($"Added cuteContentSyncApi chained entry with key: '{key}' to {value.Schedule}.");
            }

            // Iterate through chainedEntryTracker and remove entries that are not present in chainedSyncApiEntries.
            foreach (var (k, _) in chainedEntryTracker)
            {
                if (!chainedSyncApiEntries.ContainsKey(k))
                {
                    chainedEntryTracker.Remove(k);
                    _console.WriteNormal($"Removed cuteContentSyncApi chained entry with key: '{k}'.");
                }
            }

            // If the runAfterKey is not in the chainedEntries, then it is a scheduled task.
            if (!chainedSyncApiEntries.ContainsKey(runAfterKey))
            {
                if(!masterSyncApiEntries.ContainsKey(runAfterKey))
                {
                    _console.WriteException(new Exception($"cuteContentSyncApi entry with key: '{key}' is chained to runafter '{runAfterKey}', but '{runAfterKey}' is not scheduled to run."));
                    continue;
                }

                if (!result.ContainsKey(runAfterKey))
                {
                    result.Add(runAfterKey, new LinkedList<ScheduledEntry>());
                }

                // add primary descendant to the start of the list.
                result[runAfterKey].AddFirst(chainedEntryTracker[key]);
            }
            else
            {
                HashSet<string> visited = new();
                var circuit = false;
                while (chainedSyncApiEntries.ContainsKey(runAfterKey))
                {
                    if(visited.Contains(runAfterKey))
                    {
                        _console.WriteException(new Exception($"Circular dependency detected in chained cuteContentSyncApi entry with key: '{key}' run after: {string.Join('>', visited.Select(t => $"'{t}'"))}."));
                        circuit = true;
                        break;
                    }
                    visited.Add(runAfterKey);
                    runAfterKey = chainedSyncApiEntries[runAfterKey].Schedule.Replace("runafter:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                }
                
                if(circuit)
                {
                    continue;
                }

                if (!result.ContainsKey(runAfterKey))
                {
                    result.Add(runAfterKey, new LinkedList<ScheduledEntry>());
                }
                // add chained descendant to the end of the list.
                result[runAfterKey].AddLast(chainedEntryTracker[key]);
            }
        }

        _chainedStructure = new(result);
    }
}