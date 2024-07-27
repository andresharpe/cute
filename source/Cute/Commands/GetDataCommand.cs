using Contentful.Core.Errors;
using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.GetDataAdapter;
using Cute.Lib.Serializers;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Nox.Cron;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cute.Commands;

// getdata

public class GetDataCommand : LoggedInCommand<GetDataCommand.Settings>
{
    private readonly ILogger<GetDataCommand> _logger;

    public GetDataCommand(IConsoleWriter console, IPersistedTokenCache tokenCache, ILogger<GetDataCommand> logger)
        : base(console, tokenCache, logger)
    {
        _logger = logger;
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

        var locales = await _contentfulManagementClient.GetLocalesCollection();

        var defaultLocale = locales
            .First(l => l.Default)
            .Code;

        // Get data entries

        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var getDataQuery = new QueryBuilder<Entry<JObject>>()
             .ContentTypeIs(settings.GetDataContentType)
             .OrderBy("fields.order");

        if (settings.GetDataId != null)
        {
            getDataQuery.FieldEquals($"fields.{settings.GetDataIdField}", settings.GetDataId);
        }

        var getDataEntries = await _contentfulManagementClient.GetEntriesCollection(getDataQuery);

        if (!getDataEntries.Any())
        {
            throw new CliException($"No data sync entries found.");
        }

        var dataAdapter = new HttpDataAdapter(_contentfulManagementClient, _console.WriteNormal);

        foreach (var getDataEntry in getDataEntries)
        {
            var getDataId = getDataEntry.Fields[settings.GetDataIdField]?[defaultLocale]?.Value<string>();

            if (getDataId is null) continue;

            var yaml = getDataEntry.Fields[settings.GetDataYamlField]?[defaultLocale]?.Value<string>();

            var frequency = getDataEntry.Fields[settings.GetDataFrequencyField]?[defaultLocale]?.Value<string>();

            var cronSchedule = frequency?.ToCronExpression().ToString();

            var adapter = yamlDeserializer.Deserialize<HttpDataAdapterConfig>(yaml!);

            var contentType = await _contentfulManagementClient.GetContentType(adapter.ContentType);

            var serializer = new EntrySerializer(contentType, locales.Items);

            ValidateDataAdapter(getDataId!, adapter, contentType, serializer);

            var dataResults = await dataAdapter.GetData(adapter);

            var ignoreFields = adapter.Mapping.Where(m => !m.Overwrite).Select(m => m.FieldName).ToHashSet();

            if (dataResults is null) return -1;

            _console.WriteNormal($"{dataResults.Count} new {contentType.SystemProperties.Id} entries...");

            _ = await CompareAndUpdateResults(
                dataResults, serializer,
                contentTypeId: adapter.ContentType,
                contentKeyField: adapter.ContentKeyField,
                contentDisplayField: adapter.ContentDisplayField,
                ignoreFields: ignoreFields
            );
        }

        return 0;
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
        if (_contentfulManagementClient is null) return [];

        var entriesProcessed = new Dictionary<string, string>();

        await foreach (var (entry, entries) in ContentfulEntryEnumerator.Entries(_contentfulManagementClient, contentTypeId, contentKeyField))
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
        _ = await _contentfulManagementClient!.CreateOrUpdateEntry<JObject>(
                newEntry.Fields,
                id: newEntry.SystemProperties.Id,
                version: newEntry.SystemProperties.Version,
                contentTypeId: contentType);

        try
        {
            await _contentfulManagementClient.PublishEntry(newEntry.SystemProperties.Id, newEntry.SystemProperties.Version!.Value + 1);
        }
        catch (ContentfulException ex)
        {
            _console.WriteAlert($"   --> Not published ({ex.Message})");
        }
    }
}