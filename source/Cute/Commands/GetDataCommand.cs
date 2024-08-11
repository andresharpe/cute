using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Cache;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Exceptions;
using Cute.Lib.GetDataAdapter;
using Cute.Lib.Serializers;
using Cute.Services;
using Microsoft.AspNetCore.Mvc;
using NCrontab;
using NCrontab.Scheduler;
using Newtonsoft.Json.Linq;
using Nox.Cron;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cute.Commands;

public sealed class GetDataCommand : WebCommand<GetDataCommand.Settings>
{
    private readonly ILogger<GetDataCommand> _logger;

    private readonly ILogger<Scheduler> _cronLogger;

    private readonly BulkActionExecutor _bulkActionExecutor;
    private readonly HttpResponseFileCache _httpResponseCache;
    private readonly Scheduler _scheduler;

    private Dictionary<Guid, Entry<JObject>> _cronTasks = [];

    private Settings? _settings;

    public GetDataCommand(IConsoleWriter console, ILogger<GetDataCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings, ILogger<Scheduler> cronLogger,
        BulkActionExecutor bulkActionExecutor, HttpResponseFileCache httpResponseCache)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _logger = logger;
        _cronLogger = cronLogger;
        _bulkActionExecutor = bulkActionExecutor;
        _httpResponseCache = httpResponseCache;
        _scheduler = new Scheduler(_cronLogger,
            new SchedulerOptions
            {
                DateTimeKind = DateTimeKind.Utc
            }
        );
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--getdata-content-type")]
        [Description("The id of the content type containing data sync definitions. Default is 'metaGetData'.")]
        public string GetDataContentType { get; set; } = "metaGetData";

        [CommandOption("-f|--getdata-id-field")]
        [Description("The id of the field that contains the data sync key/title/id. Default is 'key'.")]
        public string GetDataIdField { get; set; } = "key";

        [CommandOption("-r|--getdata-frequency-field")]
        [Description("The id of the field that contains the data sync frequency as a phrase. Default is 'frequency'.")]
        public string GetDataFrequencyField { get; set; } = "frequency";

        [CommandOption("-i|--getdata-id")]
        [Description("The id of the Contentful data sync entry to generate prompts from.")]
        public string? GetDataId { get; set; } = default!;

        [CommandOption("-y|--getdata-yaml-field")]
        [Description("The field containing the yaml template for the the data sync.")]
        public string GetDataYamlField { get; set; } = "yaml";

        [CommandOption("--as-server")]
        [Description("The field containing the yaml template for the the data sync.")]
        public bool AsServer { get; set; } = false;

        [CommandOption("-p|--port")]
        [Description("The port to listen on")]
        public int Port { get; set; } = 8084;

        [CommandOption("-u|--use-filecache")]
        [Description("Whether or not to cache responses to a local file cache")]
        public bool UseFileCache { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _ = await base.ExecuteAsync(context, settings);

        _settings = settings;

        _logger.LogInformation("Starting command {command}", "getdata");

        // Get data entries

        await LoadGetDataEntries();

        if (settings.AsServer)
        {
            await RunAsServer();

            return 0;
        }

        DisplaySchedule();

        foreach (var getDataEntry in _cronTasks.Values)
        {
            await ProcessGetDataEntry(getDataEntry, settings);
        }

        _console.WriteBlankLine();
        _console.WriteHeading("Completed 'getdata'.");

        return 0;
    }

    private async Task LoadGetDataEntries()
    {
        if (_settings is null) return;

        var settings = _settings;

        var getDataQuery = new QueryBuilder<Entry<JObject>>()
            .ContentTypeIs(settings.GetDataContentType)
            .OrderBy("fields.order");

        if (settings.GetDataId != null)
        {
            getDataQuery.FieldEquals($"fields.{settings.GetDataIdField}", settings.GetDataId);
        }

        var cronTasks = await ContentfulManagementClient.GetEntriesCollection(getDataQuery);

        if (!cronTasks.Any())
        {
            throw new CliException($"No data sync entries found.");
        }

        _cronTasks = cronTasks.ToDictionary(t => Guid.NewGuid(), t => t);
    }

    private async Task RunAsServer()
    {
        DisplaySchedule();

        RefreshScheduler();

        await StartWebServer();
    }

    private void RefreshScheduler()
    {
        if (_settings is null) return;

        var settings = _settings;

        _scheduler.Stop();

        _scheduler.RemoveAllTasks();

        foreach (var getDataEntry in _cronTasks)
        {
            var getDataId = GetString(getDataEntry.Value, settings.GetDataIdField);

            if (getDataId is null) continue;

            var frequency = GetString(getDataEntry.Value, settings.GetDataFrequencyField);

            if (frequency is null) continue;

            var cronSchedule = frequency.ToCronExpression().ToString();

            var scheduledTask = new AsyncScheduledTask(
                getDataEntry.Key,
                CrontabSchedule.Parse(cronSchedule),
                async ct => await ProcessGetDataEntryAndDisplaySchedule(getDataEntry.Value, settings)
            );

            _scheduler.AddTask(scheduledTask);
        }

        _scheduler.Start();
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
        webApp.MapPost("/reload", RefreshSchedule).DisableAntiforgery();
    }

    public override async Task RenderHomePageBody(HttpContext context)
    {
        if (_settings is null) return;

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
            await context.Response.WriteAsync($"<td>{GetString(entry, _settings.GetDataIdField)}</td>");
            await context.Response.WriteAsync($"<td>{GetString(entry, _settings.GetDataFrequencyField)}</td>");
            await context.Response.WriteAsync($"<td>{GetString(entry, _settings.GetDataFrequencyField)?.ToCronExpression().ToString()}</td>");
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

    private async Task RefreshSchedule([FromForm] string command, HttpContext context)
    {
        if (!command.Equals("reload")) return;

        await LoadGetDataEntries();

        RefreshScheduler();

        context.Response.Redirect("/");
    }

    private async Task ProcessGetDataEntryAndDisplaySchedule(Entry<JObject> entry, Settings settings)
    {
        await ProcessGetDataEntry(entry, settings);

        DisplaySchedule();
    }

    private void DisplaySchedule()
    {
        if (_settings is null) return;

        var settings = _settings;

        foreach (var entry in _cronTasks.Values)
        {
            var getDataId = GetString(entry, settings.GetDataIdField);

            if (getDataId is null) continue;

            var frequency = GetString(entry, settings.GetDataFrequencyField);

            var cronSchedule = frequency?.ToCronExpression().ToString();

            _console.WriteNormal("Get data '{getDataId}' usually scheduled to run on schedule '{frequency} ({cronSchedule})'",
                getDataId, frequency, cronSchedule);
        }
    }

    private async Task ProcessGetDataEntry(Entry<JObject> getDataEntry, Settings settings)
    {
        if (_settings is null) return;

        var dataAdapter = new HttpDataAdapter()
            .WithDisplayAction(_console.WriteDim);

        if (settings.UseFileCache)
        {
            dataAdapter.WithHttpResponseFileCache(_httpResponseCache);
        }

        var getDataId = GetString(getDataEntry, _settings.GetDataIdField);

        if (getDataId is null) return;

        AnsiConsole.Write(new Rule() { Style = Globals.StyleDim });

        _console.WriteNormal("Started getting data '{getDataId}'", getDataId);

        var yaml = GetString(getDataEntry, _settings.GetDataYamlField);

        if (yaml is null) return;

        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var adapter = yamlDeserializer.Deserialize<HttpDataAdapterConfig>(yaml);

        adapter.Id = getDataId;

        var contentType = await ContentfulManagementClient.GetContentType(adapter.ContentType);

        var serializer = new EntrySerializer(contentType, Locales);

        ValidateDataAdapter(getDataId, adapter, contentType, serializer);

        var dataResults = await dataAdapter.GetData(adapter, _appSettings.GetSettings(),
            ContentfulManagementClient, ContentfulClient);

        var ignoreFields = adapter.Mapping.Where(m => !m.Overwrite).Select(m => m.FieldName).ToHashSet();

        if (dataResults is null) return;

        _console.WriteNormal("{Count} new {Id} entries...", dataResults.Count, contentType.SystemProperties.Id);

        _ = await CompareAndUpdateResults(
            dataResults, serializer,
            contentTypeId: adapter.ContentType,
            contentKeyField: adapter.ContentKeyField,
            contentDisplayField: adapter.ContentDisplayField,
            ignoreFields: ignoreFields
        );

        _console.WriteNormal("Completed getting data for '{getDataId}'", getDataId);
        _console.WriteBlankLine();
    }

    private static void ValidateDataAdapter(string fileName, HttpDataAdapterConfig adapter, ContentType contentType, EntrySerializer serializer)
    {
        var fields = serializer.ColumnFieldNames.ToHashSet();

        foreach (var mapping in adapter.Mapping)
        {
            if (!fields.Contains(mapping.FieldName))
            {
                throw new CliException($"Field '{mapping.FieldName}' not found in contentType '{contentType.SystemProperties.Id}' (File: '{fileName}')");
            }
        }
    }

    private async Task<Dictionary<string, string>> CompareAndUpdateResults(List<Dictionary<string, string>> newRecords, EntrySerializer contentSerializer,
         string contentTypeId, string contentKeyField, string contentDisplayField, HashSet<string> ignoreFields)
    {
        if (ContentfulManagementClient is null) return [];

        var entriesProcessed = new Dictionary<string, string>();
        var entriesUpdated = new List<Entry<JObject>>();

        await foreach (var (entry, entries) in ContentfulEntryEnumerator.Entries<Entry<JObject>>(ContentfulManagementClient, contentTypeId, contentKeyField))
        {
            /*
            if (entry.SystemProperties.PublishedAt is null)
            {
                continue;
            }
            */

            var cfEntry = contentSerializer.SerializeEntry(entry);

            if (cfEntry is null) continue;

            var key = cfEntry[contentKeyField]?.ToString();

            if (key is null) continue;

            if (!entriesProcessed.TryAdd(key, entry.SystemProperties.Id))
            {
                _console.WriteAlert($"The field '{contentKeyField}' has a duplicate '{key}' ({entry.SystemProperties.Id}). The duplicate entry Id is '{entriesProcessed[key]}'.");
                continue;
            }

            var newRecord = newRecords.FirstOrDefault(c => c[contentKeyField] == key);

            if (newRecord is null) continue;

            var entryName = newRecord[contentDisplayField];

            _console.WriteNormal("Contentful {contentTypeId} '{key}' matched with new entry '{entryName}'", contentTypeId, key, entryName);

            var isChanged = false;
            Dictionary<string, (string?, string?)> changedFields = [];

            foreach (var (fieldName, value) in newRecord)
            {
                if (ignoreFields.Contains(fieldName)) continue;

                string? oldValue = null;

                if (cfEntry.TryGetValue(fieldName, out var oldValueObj))
                {
                    oldValue = cfEntry[fieldName]?.ToString();
                }

                var isFieldChanged = contentSerializer.CompareAndUpdateEntry(cfEntry, fieldName, value);

                if (isFieldChanged)
                {
                    changedFields.Add(fieldName, (oldValue, value));
                }

                isChanged = isFieldChanged || isChanged;
            }

            if (isChanged)
            {
                var newEntryName = newRecord[contentDisplayField];

                foreach (var (fieldname, value) in changedFields)
                {
                    var fieldnameDisplay = fieldname.EscapeMarkup();
                    var valueBefore = value.Item1.EscapeMarkup();
                    var valueAfter = value.Item2.EscapeMarkup();

                    _console.WriteNormal("...field '{fieldnameDisplay}' changed from '{valueBefore}' to '{valueAfter}'", fieldnameDisplay, valueBefore, valueAfter);
                }

                entriesUpdated.Add(contentSerializer.DeserializeEntry(cfEntry));
            }
        }

        foreach (var newRecord in newRecords)
        {
            if (entriesProcessed.ContainsKey(newRecord[contentKeyField])) continue;

            var newContentfulRecord = contentSerializer.CreateNewFlatEntry();

            foreach (var (fieldName, value) in newRecord)
            {
                if (fieldName == "sys.Id" && string.IsNullOrEmpty(value))
                {
                    continue;
                }

                newContentfulRecord[fieldName] = value;
            }

            var newId = newContentfulRecord["sys.Id"]?.ToString() ?? "(error)";

            var newEntry = contentSerializer.DeserializeEntry(newContentfulRecord);

            _console.WriteNormal("Creating {contentTypeId} '{contentKeyField}' - '{contentDisplayField}'",
                contentTypeId, newRecord[contentKeyField], newRecord[contentDisplayField]);

            entriesUpdated.Add(newEntry);

            entriesProcessed.Add(newRecord[contentKeyField], newId);
        }

        await UpdateEntries(contentTypeId, entriesUpdated);

        await PublishEntries(contentTypeId, entriesUpdated);

        return entriesProcessed;
    }

    private async Task UpdateEntries(string contentTypeId, List<Entry<JObject>> entries)
    {
        if (entries.Count == 0) return;

        var count = entries.Count;

        _console.WriteNormalWithHighlights($"Publishing {count} '{contentTypeId}' entries...", Globals.StyleHeading);

        await Task.Delay(2000);

        await _bulkActionExecutor
            .WithContentType(contentTypeId)
            .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
            .WithNewEntries(entries)
            .WithConcurrentTaskLimit(25)
            .WithPublishChunkSize(100)
            .WithMillisecondsBetweenCalls(120)
            .Execute(BulkAction.Upsert);
    }

    private async Task PublishEntries(string contentTypeId, List<Entry<JObject>> entries)
    {
        if (entries.Count == 0) return;

        var count = entries.Count;

        _console.WriteNormalWithHighlights($"Publishing {count} '{contentTypeId}' entries...", Globals.StyleHeading);

        await Task.Delay(2000);

        await _bulkActionExecutor
            .WithContentType(contentTypeId)
            .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
            .WithEntries(entries.Select(entry => new BulkItem()
            {
                Sys = new Sys()
                {
                    Id = entry.SystemProperties.Id,
                    PublishedAt = entry.SystemProperties.PublishedAt,
                    ArchivedVersion = entry.SystemProperties.ArchivedVersion,
                    PublishedVersion = entry.SystemProperties.PublishedVersion,
                    Version = entry.SystemProperties.Version + 1,
                }
            }
            ).ToList())
            .WithConcurrentTaskLimit(25)
            .WithPublishChunkSize(100)
            .WithMillisecondsBetweenCalls(120)
            .Execute(BulkAction.Publish);
    }
}