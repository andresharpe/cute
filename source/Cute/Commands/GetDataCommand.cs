using Contentful.Core.Errors;
using Contentful.Core.Models;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.GetDataAdapter;
using Cute.Lib.Serializers;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cute.Commands;

// getdata --path ..\..\tests\Cute.Tests\getDataTests\ --cache-seconds 3600

public class GetDataCommand : LoggedInCommand<GetDataCommand.Settings>
{
    public GetDataCommand(IConsoleWriter console, IPersistedTokenCache tokenCache)
        : base(console, tokenCache)
    { }

    public class Settings : CommandSettings
    {
        [CommandOption("-p|--path")]
        [Description("The local path to the YAML file(s) containg the get data definitions")]
        public string Path { get; set; } = @".\";

        [CommandOption("-s|--cache-seconds")]
        [Description("The local path to the YAML file(s) containg the get data definitions")]
        public int CacheSeconds { get; set; } = 0;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (!Path.Exists(settings.Path))
        {
            return ValidationResult.Error($"Path not found '{settings.Path}'");
        }

        var files = Directory.GetFiles(settings.Path, "*.yaml");

        if (files.Length == 0)
        {
            return ValidationResult.Error($"No yaml definition files found in '{settings.Path}'");
        }

        if (settings.CacheSeconds < 0)
        {
            return ValidationResult.Error($"Invalid seconds setting for caches '{settings.CacheSeconds}'");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        if (result != 0 || _contentfulManagementClient == null || _appSettings == null) return result;

        // Locales

        var locales = await _contentfulManagementClient.GetLocalesCollection();

        var yaml = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var files = Directory.GetFiles(settings.Path, "*.yaml").OrderBy(f => f).ToArray();

        var dataAdapter = new HttpDataAdapter(_contentfulManagementClient, _console.WriteNormal);

        foreach (var fileName in files)
        {
            var adapter = yaml.Deserialize<HttpDataAdapterConfig>(System.IO.File.ReadAllText(fileName));

            var contentType = await _contentfulManagementClient.GetContentType(adapter.ContentType);

            var serializer = new EntrySerializer(contentType, locales.Items);

            ValidateDataAdapter(fileName, adapter, contentType, serializer);

            var dataResults = await dataAdapter.GetData(adapter, settings.CacheSeconds == 0 ? null : _appSettings.TempFolder, settings.CacheSeconds);

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