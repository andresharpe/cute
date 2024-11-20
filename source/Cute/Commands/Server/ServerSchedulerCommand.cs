using Contentful.Core.Search;
using Cute.Commands.BaseCommands;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.CommandModels.Schedule;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Services;
using Microsoft.AspNetCore.Mvc;
using NCrontab;
using NCrontab.Scheduler;
using Newtonsoft.Json.Linq;
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
        [Description("CuteSchedule key.")]
        public string Key { get; set; } = default!;
    }

    private class ScheduledEntry : CuteSchedule
    {
        public ScheduledEntry? RunNext = null;

        public bool IsValidSchedule { get; private set; } = true;

        public ScheduledEntry(CuteSchedule entry)
        {
            Sys = entry.Sys;
            Key = entry.Key;
            LastRunStatus = entry.LastRunStatus;
            LastRunErrorMessage = entry.LastRunErrorMessage;
            LastRunStarted = entry.LastRunStarted;
            LastRunFinished = entry.LastRunFinished;
            LastRunDuration = entry.LastRunDuration;
            UpdateEntry(entry);
        }

        public void UpdateEntry(CuteSchedule entry)
        {
            Command = entry.Command;
            Schedule = entry.Schedule;
            CronSchedule = entry.CronSchedule;
            RunAfter = entry.RunAfter;
            if (IsTimeScheduled)
            {
                var cronSchedule = Schedule.ToCronExpression();
                IsValidSchedule = cronSchedule.IsFullyParsed();
                CronSchedule = cronSchedule.ToString();
            }
        }

        public HashSet<string> GetCircularDependencies()
        {
            HashSet<string> chainedKeys = new([Key]);
            var entry = RunNext;
            while (entry is not null)
            {
                if (chainedKeys.Contains(entry.Key))
                {
                    return chainedKeys;
                }
                chainedKeys.Add(entry.Key);
                entry = entry.RunNext;
            }
            return [];
        }
    }

    private static readonly string[] AllowedCommands = { "cute content generate", "cute content sync-api", "cute content seed-geo", "cute content join", "cute content edit" };

    private Settings? _settings;

    private readonly ILogger<Scheduler> _cronLogger = cronLogger;

    private static Scheduler _scheduler = null!;

    private static readonly object _schedulerLock = new();

    private static readonly ConcurrentDictionary<Guid, ScheduledEntry> _scheduledEntries = [];

    private static ScheduledEntry? GetScheduleByKey(string id) =>
        _scheduledEntries.Values.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        _settings = settings;

        EnsureNewScheduler(_cronLogger);

        UpdateScheduler();

        _scheduler.Start();

        await StartWebServer(settings);

        return 0;
    }

    public override void ConfigureWebApplication(WebApplication webApp)
    {
        webApp.MapPost("/reload", RefreshSchedule).DisableAntiforgery();
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

        foreach (var (key, cronEntry) in _scheduledEntries.Where(k => nextRuns.ContainsKey(k.Key)).OrderBy(kv => nextRuns[kv.Key]))
        {
            var entry = cronEntry;

            while (entry is not null)
            {
                await RenderHomePageTableLines(context, entry, nextRuns[key]);
                entry = entry.RunNext;
            }
        }

        await context.Response.WriteAsync($"</table>");

        await context.Response.WriteAsync($"<form action='/reload' method='POST' enctype='multipart/form-data'>");
        await context.Response.WriteAsync($"<input type='hidden' name='command' value='reload'>");
        await context.Response.WriteAsync($"<button type='submit' style='width:100%'>Reload schedule from Contentful</button>");
        await context.Response.WriteAsync($"</form>");
    }

    private static async Task RenderHomePageTableLines(HttpContext context, ScheduledEntry entry, DateTime nextRunTime)
    {
        string? lastRunStarted = entry.LastRunStarted?.ToString("R");
        string? lastRunFinished = entry.LastRunFinished?.ToString("R");
        string? status = entry.LastRunStatus;
        string? nextRun = nextRunTime.ToString("R");

        string? cronExpression = entry.IsRunAfter ? null : entry.Schedule?.ToCronExpression().ToString();
        var schedule = entry.IsRunAfter ? "Run after " + entry.RunAfter!.Key : entry.Schedule;

        await context.Response.WriteAsync($"<tr>");
        await context.Response.WriteAsync($"<td>{entry.Key}</td>");
        await context.Response.WriteAsync($"<td>{schedule}</td>");
        await context.Response.WriteAsync($"<td>{cronExpression}</td>");
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

    private void UpdateNextRunLinks()
    {
        foreach (var entry in _scheduledEntries.Values)
        {
            entry.RunNext = null;
        }

        foreach (var entry in _scheduledEntries.Values)
        {
            if (entry.IsRunAfter)
            {
                var targetId = entry.RunAfter!.Id;
                var targetEntry = GetScheduleByKey(targetId);
                if (targetEntry is null)
                {
                    _console.WriteException(new CliException($"Run after Id '{targetId}' not found for entry: '{entry.Key}'"));
                    continue;
                }
                targetEntry.RunNext = entry;
            }
        }

        HashSet<string> allCircularKeys = [];
        foreach (var entry in _scheduledEntries.Values)
        {
            if (allCircularKeys.Contains(entry.Key)) continue;

            var circularKeys = entry.GetCircularDependencies();
            if (circularKeys.Count != 0)
            {
                _console.WriteException(new CliException($"Circular dependency detected for cuteContentSync entries: {string.Join(" > ", circularKeys)}"));
            }
            allCircularKeys.UnionWith(circularKeys);
        }
    }

    private void UpdateScheduler()
    {
        // Scheduled
        var syncApiEntries = GetSyncApiEntries()
            .ToDictionary(t => t.Key);

        Func<string, bool> isAllowed = command =>
        {
            var allowed = AllowedCommands
            .Any(allowedCommand => command
                .Trim()
                .Replace("  ", " ")
                .StartsWith(allowedCommand, StringComparison.OrdinalIgnoreCase)
            );

            if (!allowed)
            {
                _console.WriteAlert($"Command '{command}' is not allowed.");
            }

            return allowed;
        };

        lock (_schedulerLock)
        {
            foreach (var (key, entry) in _scheduledEntries)
            {
                bool removeSchedule = false;
                if (syncApiEntries.TryGetValue(entry.Key, out CuteSchedule? latestEntry))
                {
                    var latestScheduledEntry = new ScheduledEntry(latestEntry);
                    if (isAllowed(latestEntry.Command) == false)
                    {
                        removeSchedule = true;
                    }
                    else if (!latestScheduledEntry.Schedule.Equals(entry.Schedule, StringComparison.OrdinalIgnoreCase))
                    {
                        if (latestScheduledEntry.IsTimeScheduled && latestScheduledEntry.IsValidSchedule)
                        {
                            _scheduler.UpdateTask(key, CrontabSchedule.Parse(latestScheduledEntry.CronSchedule));
                        }
                        else
                        {
                            // When a scheduled entry is changed to a Run After entry, remove the scheduled task
                            _scheduler.RemoveTask(key);
                            if (!latestScheduledEntry.IsValidSchedule)
                            {
                                _console.WriteAlert($"Invalid schedule detected for '{latestScheduledEntry.Key}'.");
                                removeSchedule = true;
                            }
                        }
                        _scheduledEntries[key].UpdateEntry(latestEntry);
                    }
                    syncApiEntries.Remove(entry.Key);
                }
                else
                {
                    removeSchedule = true;
                }

                if (removeSchedule)
                {
                    _scheduledEntries.Remove(key, out _);
                    if (entry.IsTimeScheduled)
                    {
                        _scheduler.RemoveTask(key);
                    }
                }
            }

            foreach (var entry in syncApiEntries.Values.Where(entry => isAllowed(entry.Command)))
            {
                var key = Guid.NewGuid();
                var scheduledEntry = new ScheduledEntry(entry);
                if (scheduledEntry.IsTimeScheduled)
                {
                    if(scheduledEntry.IsValidSchedule == false)
                    {
                        _console.WriteAlert($"Invalid schedule detected for '{scheduledEntry.Key}'.");
                        continue;
                    }
                    _scheduler.AddTask(new AsyncScheduledTask(key, CrontabSchedule.Parse(scheduledEntry.CronSchedule), ct => Task.Run(() => ProcessAndUpdateSchedule(_scheduledEntries[key]), ct)));
                }
                _scheduledEntries[key] = scheduledEntry;
            }
        }

        UpdateNextRunLinks();

        DisplaySchedule();
    }

    private CuteSchedule[] GetSyncApiEntries()
    {
        if (_settings is null) throw new CliException("Settings can't be null");

        CuteSchedule? specifiedJob = null;

        if (!string.IsNullOrEmpty(_settings.Key))
        {
            specifiedJob = ContentfulConnection.GetPreviewEntryByKey<CuteSchedule>(_settings.Key)
                ?? throw new CliException($"No API sync entry with key '{_settings.Key}' was found.");
        }

        var cronTasksList =
            (
                specifiedJob is null
                ? ContentfulConnection.GetAllPreviewEntries<CuteSchedule>().ToArray()
                : [specifiedJob]
            )
            .Where(cronTask => !string.IsNullOrEmpty(cronTask.Schedule) || cronTask.RunAfter != null)
            .Where(cronTask => !"never".Equals(cronTask.Schedule, StringComparison.OrdinalIgnoreCase) || cronTask.RunAfter != null)
            .ToList();

        var cronTasks = cronTasksList.ToArray();
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

            _scheduledEntries.Clear();
        }
    }

    private void RefreshSchedule([FromForm] string command, HttpContext context)
    {
        if (!command.Equals("reload")) return;

        EnsureNewScheduler(_cronLogger);

        UpdateScheduler();

        _scheduler.Start();

        context.Response.Redirect("/");
    }

    private async Task ProcessAndUpdateSchedule(ScheduledEntry entry)
    {
        await ProcessContentSyncApi(entry);

        UpdateScheduler();
    }

    private void DisplaySchedule()
    {
        _console.WriteNormal("Schedule loaded at {now} ({nowFriendly})", DateTime.UtcNow.ToString("O"), DateTime.UtcNow.ToString("R"));

        foreach (var entry in _scheduledEntries.Values)
        {
            var frequency = entry.Schedule;

            _console.WriteNormal("Content sync-api '{syncApiKey}' usually scheduled to run on schedule '{frequency}'",
                entry.Key, entry.RunAfter == null ? frequency : $"after {entry.RunAfter.Key}");
        }
    }

    private async Task ProcessContentSyncApi(ScheduledEntry CuteSchedule)
    {
        string verbosity = _settings?.Verbosity.ToString() ?? Verbosity.Normal.ToString();

        var entry = CuteSchedule;

        while (entry is not null)
        {
            _console.WriteNormal("Started content sync-api for '{syncApiKey}'", entry.Key);

            entry.LastRunStarted = DateTime.UtcNow;
            entry.LastRunFinished = null;
            entry.LastRunStatus = "running";
            entry.LastRunErrorMessage = string.Empty;

            try
            {
                var splitter = System.CommandLine.Parsing.CommandLineStringSplitter.Instance;
                var parameters = splitter.Split(entry.Command).ToList();
                if (parameters[0] == "cute") parameters.RemoveAt(0);
                
                if(parameters.Any(p => p == "--apply") == false)
                {
                    parameters.Add("--apply");
                }
                if (parameters.Any(p => p == "--force") == false)
                {
                    parameters.Add("--force");
                }
                if (parameters.Any(p => p == "--log-output") == false)
                {
                    parameters.Add("--log-output");
                }
                if (parameters.Any(p => p == "--verbosity") == false)
                {
                    parameters.Add("--verbosity");
                    parameters.Add("Detailed");
                }
                var args = parameters.ToArray();
                var command = new CommandAppBuilder(args).Build();

                await command.RunAsync(args);

                entry.LastRunStatus = "success";
                entry.LastRunFinished = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _console.WriteException(ex);
                entry.LastRunStatus = $"error";
                entry.LastRunErrorMessage = ex.Message;
                entry.LastRunFinished = DateTime.UtcNow;
            }
            finally
            {
                _console.WriteNormal("Completed content sync-api for '{syncApiKey}'", entry.Key);
                await UpdateScheduleEntry(entry);
            }

            entry = entry.RunNext;
        }
    }

    private async Task UpdateScheduleEntry(ScheduledEntry scheduledEntry)
    {
        scheduledEntry.LastRunDuration = (scheduledEntry.LastRunFinished - scheduledEntry.LastRunStarted)?.ToString("g");
        var locale = (await ContentfulConnection.GetDefaultLocaleAsync()).Code;
        var remoteEntry = await ContentfulConnection.GetManagementEntryAsync(scheduledEntry.Id);
        if (remoteEntry == null)
        {
            _console.WriteAlert($"Entry not found in Contentful: {scheduledEntry.Id}");
            return;
        }

        var fields = remoteEntry.Fields as JObject;
        if(fields == null)
        {
            _console.WriteAlert($"Entry fields not found in Contentful: {scheduledEntry.Id}");
            return;
        }

        UpdateField(fields, nameof(scheduledEntry.CronSchedule).ToCamelCase(), scheduledEntry.CronSchedule, locale);
        UpdateField(fields, nameof(scheduledEntry.LastRunStatus).ToCamelCase(), scheduledEntry.LastRunStatus, locale);
        UpdateField(fields, nameof(scheduledEntry.LastRunFinished).ToCamelCase(), scheduledEntry.LastRunFinished?.ToString("yyyy-MM-ddTHH:mm:ssZ"), locale);
        UpdateField(fields, nameof(scheduledEntry.LastRunStarted).ToCamelCase(), scheduledEntry.LastRunStarted?.ToString("yyyy-MM-ddTHH:mm:ssZ"), locale);
        UpdateField(fields, nameof(scheduledEntry.LastRunDuration).ToCamelCase(), scheduledEntry.LastRunDuration, locale);
        UpdateField(fields, nameof(scheduledEntry.LastRunErrorMessage).ToCamelCase(), scheduledEntry.LastRunErrorMessage, locale);

        if(scheduledEntry.IsRunAfter && !scheduledEntry.Schedule.StartsWith("Run After", StringComparison.OrdinalIgnoreCase))
        {
            UpdateField(fields, nameof(scheduledEntry.Schedule).ToCamelCase(), $"Run After '{scheduledEntry.RunAfter!.Key}'", locale);
        }

        await ContentfulConnection.CreateOrUpdateEntryAsync(remoteEntry, remoteEntry.SystemProperties.Version);
        await ContentfulConnection.PublishEntryAsync(remoteEntry.SystemProperties.Id, remoteEntry.SystemProperties.Version!.Value + 1);
    }

    private void UpdateField(JObject fields, string fieldName, object? value, string locale)
    {
        var oldValueRef = fields[fieldName];

        if (oldValueRef is null)
        {
            oldValueRef = new JObject()
            {
                [locale] = null
            };
            fields[fieldName] = oldValueRef;
        }

        oldValueRef[locale] = value != null ? JToken.FromObject(value) : null;
    }
}