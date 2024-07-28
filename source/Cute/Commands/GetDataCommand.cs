using Contentful.Core.Errors;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Contentful.Core.Search;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.GetDataAdapter;
using Cute.Lib.Serializers;
using Cute.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NCrontab;
using NCrontab.Scheduler;
using Newtonsoft.Json.Linq;
using Nox.Cron;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cute.Commands;

public class GetDataCommand : LoggedInCommand<GetDataCommand.Settings>
{
    private readonly ILogger<GetDataCommand> _logger;

    private readonly ILogger<Scheduler> _cronLogger;

    private readonly IScheduler _scheduler;

    private Dictionary<Guid, Entry<JObject>> _cronTasks = [];

    private string _defaultLocale = string.Empty;

    public GetDataCommand(IConsoleWriter console, IPersistedTokenCache tokenCache,
        ILogger<GetDataCommand> logger, ILogger<Scheduler> cronLogger)
        : base(console, tokenCache, logger)
    {
        _logger = logger;
        _cronLogger = cronLogger;
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
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        // Locales
        _logger.LogInformation("Starting command {command}", "getdata");

        var locales = await ContentfulManagementClient.GetLocalesCollection();

        _defaultLocale = locales
            .First(l => l.Default)
            .Code;

        // Get data entries

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

        if (settings.AsServer)
        {
            return await RunAsServer(settings, locales);
        }

        foreach (var getDataEntry in _cronTasks.Values)
        {
            await ProcessGetDataEntry(getDataEntry, settings, locales);
        }

        return 0;
    }

    private async Task<int> RunAsServer(Settings settings, ContentfulCollection<Locale> locales)
    {
        DisplaySchedule(settings);

        foreach (var getDataEntry in _cronTasks)
        {
            var getDataId = getDataEntry.Value.Fields[settings.GetDataIdField]?[_defaultLocale]?.Value<string>();

            if (getDataId is null) continue;

            var frequency = getDataEntry.Value.Fields[settings.GetDataFrequencyField]?[_defaultLocale]?.Value<string>();

            var cronSchedule = frequency?.ToCronExpression().ToString();

            var scheduledTask = new AsyncScheduledTask(
                getDataEntry.Key,
                CrontabSchedule.Parse(cronSchedule),
                async ct => await ProcessGetDataEntryAndDisplaySchedule(getDataEntry.Value, settings, locales)
            );

            _scheduler.AddTask(scheduledTask);
        }

        _scheduler.Start();

        var webBuilder = WebApplication.CreateBuilder();

        webBuilder.Services.AddHealthChecks();

        webBuilder.Logging.ClearProviders().AddSerilog();

        webBuilder.WebHost.ConfigureKestrel(web =>
        {
            web.ListenLocalhost(settings.Port);
        });

        var webapp = webBuilder.Build();

        webapp.MapGet("/", DisplayHomePage);

        webapp.MapHealthChecks("/healthz");

        try
        {
            await webapp.RunAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
        }

        return 0;
    }

    private async Task ProcessGetDataEntryAndDisplaySchedule(Entry<JObject> entry, Settings settings, ContentfulCollection<Locale> locales)
    {
        await ProcessGetDataEntry(entry, settings, locales);

        DisplaySchedule(settings);
    }

    private void DisplaySchedule(Settings settings)
    {
        foreach (var getDataEntry in _cronTasks.Values)
        {
            var getDataId = getDataEntry.Fields[settings.GetDataIdField]?[_defaultLocale]?.Value<string>();

            if (getDataId is null) continue;

            var frequency = getDataEntry.Fields[settings.GetDataFrequencyField]?[_defaultLocale]?.Value<string>();

            var cronSchedule = frequency?.ToCronExpression().ToString();

            _logger.LogInformation("Get data '{getDataId}' scheduled to run on schedule '{frequency} ({cronSchedule})'",
                getDataId, frequency, cronSchedule);
        }
    }

    private async Task ProcessGetDataEntry(Entry<JObject> getDataEntry, Settings settings, ContentfulCollection<Locale> locales)
    {
        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var dataAdapter = new HttpDataAdapter(ContentfulManagementClient, _console.WriteNormal);

        var getDataId = getDataEntry.Fields[settings.GetDataIdField]?[_defaultLocale]?.Value<string>();

        if (getDataId is null) return;

        _logger.LogInformation("Started '{getDataId}'", getDataId);

        var yaml = getDataEntry.Fields[settings.GetDataYamlField]?[_defaultLocale]?.Value<string>();

        var adapter = yamlDeserializer.Deserialize<HttpDataAdapterConfig>(yaml!);

        var contentType = await ContentfulManagementClient.GetContentType(adapter.ContentType);

        var serializer = new EntrySerializer(contentType, locales.Items);

        ValidateDataAdapter(getDataId!, adapter, contentType, serializer);

        var dataResults = await dataAdapter.GetData(adapter);

        var ignoreFields = adapter.Mapping.Where(m => !m.Overwrite).Select(m => m.FieldName).ToHashSet();

        if (dataResults is null) return;

        _console.WriteNormal($"{dataResults.Count} new {contentType.SystemProperties.Id} entries...");

        _ = await CompareAndUpdateResults(
            dataResults, serializer,
            contentTypeId: adapter.ContentType,
            contentKeyField: adapter.ContentKeyField,
            contentDisplayField: adapter.ContentDisplayField,
            ignoreFields: ignoreFields
        );

        _logger.LogInformation("Completed '{getDataId}'", getDataId);
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

        await foreach (var (entry, entries) in ContentfulEntryEnumerator.Entries(ContentfulManagementClient, contentTypeId, contentKeyField))
        {
            var cfEntry = contentSerializer.SerializeEntry(entry);

            if (cfEntry is null) continue;

            var key = cfEntry[contentKeyField]?.ToString();

            if (key is null) continue;

            entriesProcessed.Add(key, entry.SystemProperties.Id);

            var newRecord = newRecords.FirstOrDefault(c => c[contentKeyField] == key);

            if (newRecord is null) continue;

            _console.WriteNormal($"Contentful {contentTypeId} '{key}' matched with new entry '{newRecord[contentDisplayField]}'");

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
                _console.WriteNormal($"Contentful {contentTypeId} '{key}' was updated by new entry '{newRecord[contentDisplayField]}'");
                foreach (var (fieldname, value) in changedFields)
                {
                    _console.WriteNormal($"...field '{fieldname}' changed from '{value.Item1}' to '{value.Item2}'");
                }
                await UpdateAndPublishEntry(contentSerializer.DeserializeEntry(cfEntry), contentTypeId);
            }
        }

        foreach (var newRecord in newRecords)
        {
            if (entriesProcessed.ContainsKey(newRecord[contentKeyField])) continue;

            var newContentfulRecord = contentSerializer.CreateNewFlatEntry();

            var newLanguageId = newContentfulRecord["sys.Id"]?.ToString() ?? "(error)";

            foreach (var (fieldName, value) in newRecord)
            {
                newContentfulRecord[fieldName] = value;
            }

            var newEntry = contentSerializer.DeserializeEntry(newContentfulRecord);

            _console.WriteNormal($"Creating {contentTypeId} '{newRecord[contentKeyField]}' - '{newRecord[contentDisplayField]}'");

            await UpdateAndPublishEntry(newEntry, contentTypeId);

            entriesProcessed.Add(newRecord[contentKeyField], newLanguageId);
        }

        return entriesProcessed;
    }

    private async Task UpdateAndPublishEntry(Entry<JObject> newEntry, string contentType)
    {
        _ = await ContentfulManagementClient!.CreateOrUpdateEntry<JObject>(
                newEntry.Fields,
                id: newEntry.SystemProperties.Id,
                version: newEntry.SystemProperties.Version,
                contentTypeId: contentType);

        try
        {
            await ContentfulManagementClient.PublishEntry(newEntry.SystemProperties.Id, newEntry.SystemProperties.Version!.Value + 1);
        }
        catch (ContentfulException ex)
        {
            _console.WriteAlert($"   --> Not published ({ex.Message})");
        }
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

                await context.Response.WriteAsync($"<tr>");
            }

            await context.Response.WriteAsync($"</table>");
        }

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

        foreach (var getDataEntry in _cronTasks)
        {
            await context.Response.WriteAsync($"<tr>");
            await context.Response.WriteAsync($"<td>{getDataEntry.Value.Fields["key"]?[_defaultLocale]}</td>");
            await context.Response.WriteAsync($"<td>{getDataEntry.Value.Fields["frequency"]?[_defaultLocale]}</td>");
            await context.Response.WriteAsync($"<td>{getDataEntry.Value.Fields["frequency"]?[_defaultLocale]?.Value<string>()?.ToCronExpression().ToString()}</td>");
            await context.Response.WriteAsync($"<td>");
            await context.Response.WriteAsync($"{nextRun[getDataEntry.Key].ToString("R")}<br>");
            await context.Response.WriteAsync($"</td>");
            await context.Response.WriteAsync($"</tr>");
        }

        await context.Response.WriteAsync($"</table>");
        var htmlEnd = $"""
                <footer><a href="{Globals.AppMoreInfo}"><i style="font-size:20px" class="fa">&#xf09b;</i>&nbsp;&nbsp;Source code on GitHub</a></footer>
              </body>
            </html>
            """;

        await context.Response.WriteAsync(htmlEnd);

        return;
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
}