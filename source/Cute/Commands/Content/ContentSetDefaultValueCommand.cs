using Contentful.Core.Models;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.InputAdapters.EntryAdapters;
using Cute.Lib.Scriban;
using Cute.Lib.Serializers;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using static Cute.Commands.Content.ContentSetDefaultValueCommand;

namespace Cute.Commands.Content;

public class ContentSetDefaultValueCommand(IConsoleWriter console, ILogger<ContentSetDefaultValueCommand> logger,
    AppSettings appSettings, HttpClient httpClient, ContentfulConnection contentfulConnection)
    : BaseLoggedInCommand<Settings>(console, logger, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ContentfulConnection _contentfulConnection = contentfulConnection;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-c|--content-type-id <ID>")]
        [Description("The Contentful content type ID.")]
        public string ContentTypeId { get; set; } = default!;

        [CommandOption("-l|--locale <CODE>")]
        [Description("The locale code (eg. 'en') to apply the command to.")]
        public string Locale { get; set; } = default!;

        [CommandOption("-f|--field")]
        [Description("The fields to update.")]
        public string[] Fields { get; set; } = null!;

        [CommandOption("-r|--replace")]
        [Description("The values to update it with. Can contain an expression.")]
        public string[] Values { get; set; } = null!;

        [CommandOption("--filter-field")]
        [Description("The field to update.")]
        public string filterField { get; set; } = null!;

        [CommandOption("--filter-field-value")]
        [Description("The value to update it with. Can contain an expression.")]
        public string filterFieldValue { get; set; } = null!;

        [CommandOption("-a|--apply")]
        [Description("Apply and publish all the required edits. The default behaviour is to only list the detected changes.")]
        public bool Apply { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        settings.Locale ??= _contentfulConnection.GetDefaultLocaleAsync().Result.Code;

        settings.Locale = settings.Locale.ToLower();

        if (settings.Fields.Length != settings.Values.Length)
        {
            return ValidationResult.Error($"Mismatch in field ({settings.Fields.Length}) and value ({settings.Values.Length}) count.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        settings.ContentTypeId = await ResolveContentTypeId(settings.ContentTypeId) ??
            throw new CliException("You need to specify a content type to edit.");

        var contentType = await GetContentTypeOrThrowError(settings.ContentTypeId);

        var matchedFields = contentType.Fields
            .Select(f => f.Id)
            .Intersect(settings.Fields)
            .ToHashSet();

        var missingFields = settings.Fields
            .Where(f => !matchedFields.Contains(f))
            .ToArray();

        if (missingFields.Length > 0)
        {
            throw new CliException($"The field(s) named '{string.Join(',', missingFields)}' not in content type '{settings.ContentTypeId}'");
        }

        var locales = await ContentfulConnection.GetLocalesAsync();

        var locale = locales.FirstOrDefault(l => l.Code.Equals(settings.Locale));

        if (locale == null)
        {
            throw new CliException($"The locale '{settings.Locale}' was not found.");
        }

        if (settings.Apply && !ConfirmWithPromptChallenge($"{"EDIT"} all entries in '{settings.ContentTypeId}'"))
        {
            return -1;
        }

        string queryString = string.Empty;
        if(!string.IsNullOrEmpty(settings.filterField))
        {
            if (string.IsNullOrEmpty(settings.filterFieldValue))
            {
                throw new CliException($"The filter field value is required when using the filter field.");
            }
            else
            {
                queryString = $"fields.{settings.filterField}={settings.filterFieldValue}";
            }
        }

        var contentLocales = await ContentfulConnection.GetContentLocalesAsync();

        var fieldMap = contentType.Fields.ToDictionary(
            k =>
            {
                if (!Enum.TryParse(k!.Type, out FieldType fieldType))
                {
                    throw new CliException($"The field '{k.Id}' is not a valid field type.");
                }
                return $"{k.Id}.{locale.Code}{(fieldType == FieldType.Array ? "[]" : string.Empty)}";
            },
            k => k.Id);

        var serializer = new EntrySerializer(contentType, contentLocales);

        List<IDictionary<string, object?>> flatEntries = new List<IDictionary<string, object?>>();

        ScriptObject? scriptObject = [];
        CuteFunctions.ContentfulConnection = _contentfulConnection;
        scriptObject.SetValue("cute", new CuteFunctions(), true);

        var enumerable = _contentfulConnection.GetManagementEntries<Entry<JObject>>(
            new EntryQuery.Builder()
        .WithContentType(contentType)
        .WithQueryString(queryString)
        .WithIncludeLevels(2)
        .Build()
        );

        await foreach (var item in enumerable)
        {
            var entry = item.Entry;
            var serializedEntry = serializer.SerializeEntry(entry);

            var transformedEntry = serializedEntry.ToDictionary(k => fieldMap.ContainsKey(k.Key) ? fieldMap[k.Key] : k.Key, v => v.Value);

            scriptObject.SetValue(contentType.SystemProperties.Id, transformedEntry, true);

            for ( var i = 0; i < matchedFields.Count; i++)
            {
                var matchedField = matchedFields.ElementAt(i);
                var value = settings.Values.ElementAt(i);

                if(!string.IsNullOrEmpty(transformedEntry[matchedField]?.ToString()))
                {
                    continue;
                }

                var template = Template.Parse(value);
                transformedEntry[matchedField] = template.Render(scriptObject);
            }

            var detransformedEntry = serializedEntry.ToDictionary(k => k.Key, k => transformedEntry[fieldMap.ContainsKey(k.Key) ? fieldMap[k.Key] : k.Key] ?? null);

            flatEntries.Add(detransformedEntry);
        }

        if(flatEntries.Count == 0)
        {
            Console.WriteLine($"No entries were found to update");
            return 0;
        }

        var bulkOperations = new List<IBulkAction>()
        {
            new UpsertBulkAction(ContentfulConnection, _httpClient)
                .WithContentType(contentType)
                .WithContentLocales(contentLocales)
                .WithNewEntries(flatEntries.Select(k => serializer.DeserializeEntry(k)).ToList())
                .WithApplyChanges(settings.Apply)
                .WithVerbosity(settings.Verbosity)
        };

        if (settings.Apply)
        {
            bulkOperations.Add(
                new PublishBulkAction(ContentfulConnection, _httpClient)
                .WithContentType(contentType)
                .WithContentLocales(contentLocales)
                .WithVerbosity(settings.Verbosity)
            );
        }

        await PerformBulkOperations(bulkOperations.ToArray());

        return 0;
    }
}