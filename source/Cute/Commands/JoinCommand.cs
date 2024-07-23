using Contentful.Core.Errors;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Contentful.Core.Search;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.Serializers;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands;

// generate --join-key ContentGeo.BusinessRationale

public class JoinCommand : LoggedInCommand<JoinCommand.Settings>
{
    public JoinCommand(IConsoleWriter console, IPersistedTokenCache tokenCache)
        : base(console, tokenCache)
    {
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--join-content-type")]
        [Description("The id of the content type containing join definitions. Default is 'metaJoin'.")]
        public string JoinContentType { get; set; } = "metaJoin";

        [CommandOption("-f|--join-id-field")]
        [Description("The id of the field that contains the join key/title/id. Default is 'key'.")]
        public string JoinIdField { get; set; } = "key";

        [CommandOption("-i|--join-id")]
        [Description("The id of the Contentful join entry to generate content for.")]
        public string JoinId { get; set; } = default!;

        [CommandOption("-t|--target-content-type-field")]
        [Description("The field containing the id of the Contentful content type to join content for.")]
        public string TargetContentType { get; set; } = "targetContentType";

        [CommandOption("--source1-content-type-field")]
        [Description("The field containing the first Contentful content type to join content from.")]
        public string SourceContentType1 { get; set; } = "sourceContentType1";

        [CommandOption("--source1-keys-field")]
        [Description("The field containing the keys of the first Contentful content type to join content from.")]
        public string SourceKeys1 { get; set; } = "sourceKeys1";

        [CommandOption("--source2-content-type-field")]
        [Description("The field containing the second Contentful content type to join content from.")]
        public string SourceContentType2 { get; set; } = "sourceContentType2";

        [CommandOption("--source2-keys-field")]
        [Description("The field containing the keys of the second Contentful content type to join content from.")]
        public string SourceKeys2 { get; set; } = "sourceKeys2";

        [CommandOption("-l|--limit")]
        [Description("The total number of entries to generate content for before stopping. Default is 100.")]
        public int Limit { get; set; } = 100;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.JoinId))
        {
            return ValidationResult.Error($"No content join identifier (--join-id) specified.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        var locales = await _contentfulManagementClient.GetLocalesCollection();

        var defaultLocale = locales
            .First(l => l.Default)
            .Code;

        var joinQuery = new QueryBuilder<Dictionary<string, object?>>()
             .ContentTypeIs(settings.JoinContentType)
             .Limit(1)
             .FieldEquals($"fields.{settings.JoinIdField}", settings.JoinId)
             .Build();

        var joinEntries = await _contentfulManagementClient.GetEntriesCollection<Entry<JObject>>(joinQuery);

        if (!joinEntries.Any())
        {
            throw new CliException($"No join definition with title '{settings.JoinId}' found.");
        }

        var joinEntry = joinEntries.First();

        var joinTargetContentTypeId = joinEntry.Fields[settings.TargetContentType]?[defaultLocale]?.Value<string>()
            ?? throw new CliException($"Join definition '{settings.JoinId}' does not contain a valid contentTypeId for target");

        var joinSource1ContentTypeId = joinEntry.Fields[settings.SourceContentType1]?[defaultLocale]?.Value<string>()
            ?? throw new CliException($"Join definition '{settings.JoinId}' does not contain a valid contentTypeId for source 1");

        var joinSource1Keys = joinEntry.Fields[settings.SourceKeys1]?[defaultLocale]?.ToObject<string[]>()
            ?? throw new CliException($"Join definition '{settings.JoinId}' does not contain a valid contentTypeId for source keys 1");

        var joinSource2ContentTypeId = joinEntry.Fields[settings.SourceContentType2]?[defaultLocale]?.Value<string>()
            ?? throw new CliException($"Join definition '{settings.JoinId}' does not contain a valid contentTypeId for source 2");

        var joinSource2Keys = joinEntry.Fields[settings.SourceKeys2]?[defaultLocale]?.ToObject<string[]>()
            ?? throw new CliException($"Join definition '{settings.JoinId}' does not contain a valid contentTypeId for source keys 2");

        // Load contentId's

        var source1ContentType = await _contentfulManagementClient.GetContentType(joinSource1ContentTypeId);
        var source2ContentType = await _contentfulManagementClient.GetContentType(joinSource2ContentTypeId);
        var targetContentType = await _contentfulManagementClient.GetContentType(joinTargetContentTypeId);

        var targetField1 = targetContentType.Fields
            .Where(f => f.Validations.Any(v => v is LinkContentTypeValidator vLink && vLink.ContentTypeIds.Contains(joinSource1ContentTypeId)))
            .FirstOrDefault()
            ?? throw new CliException($"No reference field for content type '{joinSource1ContentTypeId}' found in '{joinTargetContentTypeId}'");

        var targetField2 = targetContentType.Fields
            .Where(f => f.Validations.Any(v => v is LinkContentTypeValidator vLink && vLink.ContentTypeIds.Contains(joinSource2ContentTypeId)))
            .FirstOrDefault()
            ?? throw new CliException($"No reference field for content type '{joinSource2ContentTypeId}' found in '{joinTargetContentTypeId}'");

        var targetSerializer = new EntrySerializer(targetContentType, locales);

        // Load keys

        var source1Keys = joinSource1Keys.Select(k => k?.Trim()).ToHashSet();
        var source1AllKeys = source1Keys.Any(k => k == "*");
        var source2Keys = joinSource2Keys.Select(k => k?.Trim()).ToHashSet();
        var source2AllKeys = source2Keys.Any(k => k == "*");

        // Load Entries

        _console.WriteNormal("Loading entries...");

        var targetData = ContentfulEntryEnumerator.DeliveryEntries(_contentfulClient, joinTargetContentTypeId, targetContentType.DisplayField)
            .ToBlockingEnumerable()
            .ToDictionary(e => e.Item1["key"]!, e => new { Key = e.Item1["key"], Title = e.Item1["title"], Name = e.Item1["name"] });

        var source1Data = ContentfulEntryEnumerator.DeliveryEntries(_contentfulClient, joinSource1ContentTypeId, source1ContentType.DisplayField)
            .ToBlockingEnumerable()
            .Where(e => source1AllKeys || source1Keys.Contains(e.Item1["key"]?.Value<string>()));

        var source2Data = ContentfulEntryEnumerator.DeliveryEntries(_contentfulClient, joinSource2ContentTypeId, source2ContentType.DisplayField)
            .ToBlockingEnumerable()
            .Where(e => source2AllKeys || source2Keys.Contains(e.Item1["key"]?.Value<string>()));

        // Start Join

        var limit = 0;

        foreach (var (entry1, _) in source1Data)
        {
            foreach (var (entry2, _) in source2Data)
            {
                var joinKey = $"{entry1["key"]?.Value<string>()}.{entry2["key"]?.Value<string>()}";
                var joinTitle = $"{entry1["title"]?.Value<string>()} | {entry2["title"]?.Value<string>()}";
                var joinName = $"{entry2["name"]?.Value<string>()}";

                if (targetData.ContainsKey(joinKey)) continue;

                var newFlatEntry = targetSerializer.CreateNewFlatEntry();
                newFlatEntry[$"key.{defaultLocale}"] = joinKey;
                newFlatEntry[$"title.{defaultLocale}"] = joinTitle;
                newFlatEntry[$"name.{defaultLocale}"] = joinName;
                newFlatEntry[$"{targetField1.Id}.{defaultLocale}"] = entry1["$id"];
                newFlatEntry[$"{targetField2.Id}.{defaultLocale}"] = entry2["$id"];

                var newEntry = targetSerializer.DeserializeEntry(newFlatEntry);

                _console.WriteNormal($"Creating {joinTargetContentTypeId} '{joinKey}' - '{joinTitle}'");

                await UpdateAndPublishEntry(newEntry, joinTargetContentTypeId);

                if (++limit >= settings.Limit)
                {
                    break;
                }
            }
        }

        return 0;
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