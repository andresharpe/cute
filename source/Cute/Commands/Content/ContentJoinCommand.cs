using Contentful.Core.Errors;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Contentful.Core.Search;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.CommandModels.ContentJoinCommand;
using Cute.Lib.Exceptions;
using Cute.Lib.Serializers;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Content;

public class ContentJoinCommand(IConsoleWriter console, ILogger<ContentJoinCommand> logger, ContentfulConnection contentfulConnection,
    AppSettings appSettings) : BaseLoggedInCommand<ContentJoinCommand.Settings>(console, logger, contentfulConnection, appSettings)
{
    public class Settings : LoggedInSettings
    {
        [CommandOption("-i|--join-id")]
        [Description("The id of the Contentful join entry to generate content for.")]
        public string JoinId { get; set; } = default!;

        [CommandOption("--entry-id")]
        [Description("Id of source 2 entry to generate content for.")]
        public string? Source2EntryId { get; set; } = null;

        [CommandOption("-l|--limit")]
        [Description("The total number of entries to generate content for before stopping. Default is 100000.")]
        public int Limit { get; set; } = 100000;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.JoinId))
        {
            return ValidationResult.Error($"No content join identifier (--join-id) specified.");
        }

        return base.Validate(context, settings);
    }
    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var defaultLocale = Locales
            .First(l => l.Default)
            .Code;

        var joinEntry = CuteContentJoin.GetByKey(ContentfulClient, settings.JoinId);

        if (joinEntry == null)
        {
            throw new CliException($"No join definition with title '{settings.JoinId}' found.");
        }

        // Load contentId's

        var source1ContentType = GetContentTypeOrThrowError(joinEntry.SourceContentType1);
        var source2ContentType = GetContentTypeOrThrowError(joinEntry.SourceContentType2);
        var targetContentType = GetContentTypeOrThrowError(joinEntry.TargetContentType);

        var targetField1 = targetContentType.Fields
            .Where(f => f.Validations.Any(v => v is LinkContentTypeValidator vLink && vLink.ContentTypeIds.Contains(joinEntry.SourceContentType1)))
            .FirstOrDefault()
            ?? throw new CliException($"No reference field for content type '{joinEntry.SourceContentType1}' found in '{joinEntry.TargetContentType}'");

        var targetField2 = targetContentType.Fields
            .Where(f => f.Validations.Any(v => v is LinkContentTypeValidator vLink && vLink.ContentTypeIds.Contains(joinEntry.SourceContentType2)))
            .FirstOrDefault()
            ?? throw new CliException($"No reference field for content type '{joinEntry.SourceContentType2}' found in '{joinEntry.TargetContentType}'");

        var targetSerializer = new EntrySerializer(targetContentType, new ContentLocales([], "en"));//locales);

        // Load keys

        var source1Keys = joinEntry.SourceKeys1.Select(k => k?.Trim()).ToHashSet();
        var source1AllKeys = source1Keys.Any(k => k == "*");
        var source2Keys = joinEntry.SourceKeys2.Select(k => k?.Trim()).ToHashSet();
        var source2AllKeys = source2Keys.Any(k => k == "*");

        // Load Entries

        _console.WriteNormal("Loading entries...");

        Action<QueryBuilder<JObject>>? queryConfigTarget =
            string.IsNullOrEmpty(settings.Source2EntryId)
            ? null
            : b => b.FieldEquals($"fields.{targetField2.Id}.sys.id", settings.Source2EntryId);

        Action<QueryBuilder<JObject>>? queryConfigSource2 =
            string.IsNullOrEmpty(settings.Source2EntryId)
            ? null
            : b => b.FieldEquals("sys.id", settings.Source2EntryId);

        var targetData = ContentfulEntryEnumerator.DeliveryEntries(ContentfulClient, joinEntry.TargetContentType, targetContentType.DisplayField, queryConfigurator: queryConfigTarget)
            .ToBlockingEnumerable()
            .ToDictionary(e => e.Entry["key"]!, e => new { Key = e.Entry["key"], Title = e.Entry["title"], Name = e.Entry["name"] });

        var source1Data = ContentfulEntryEnumerator.DeliveryEntries<JObject>(ContentfulClient, joinEntry.SourceContentType1, source1ContentType.DisplayField)
            .ToBlockingEnumerable()
            .Where(e => source1AllKeys || source1Keys.Contains(e.Entry["key"]?.Value<string>()));

        var source2Data = ContentfulEntryEnumerator.DeliveryEntries(ContentfulClient, joinEntry.SourceContentType2, source2ContentType.DisplayField, queryConfigurator: queryConfigSource2)
            .ToBlockingEnumerable()
            .Where(e => source2AllKeys || source2Keys.Contains(e.Entry["key"]?.Value<string>()));

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

                _console.WriteNormal("Creating {joinTargetContentTypeId} '{joinKey}' - '{joinTitle}'", joinEntry.TargetContentType, joinKey, joinTitle);

                await UpdateAndPublishEntry(newEntry, joinEntry.TargetContentType);

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
        _ = await ContentfulManagementClient!.CreateOrUpdateEntry(
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
}