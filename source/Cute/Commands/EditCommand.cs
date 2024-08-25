using Contentful.Core;
using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Exceptions;
using Cute.Lib.Scriban;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands;

public sealed class EditCommand : LoggedInCommand<EditCommand.Settings>
{
    private readonly ILogger<UploadCommand> _logger;
    private readonly BulkActionExecutor _bulkActionExecutor;

    public EditCommand(IConsoleWriter console, ILogger<UploadCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings,
        BulkActionExecutor bulkActionExecutor)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _logger = logger;
        _bulkActionExecutor = bulkActionExecutor;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--content-type")]
        [Description("Specifies the content type to bulk edit.")]
        public string ContentType { get; set; } = null!;

        [CommandOption("-f|--field")]
        [Description("The field to update.")]
        public string[] Fields { get; set; } = null!;

        [CommandOption("-v|--value")]
        [Description("The format value to update it with. Can contain an expression.")]
        public string[] Values { get; set; } = null!;

        [CommandOption("-l|--locale")]
        [Description("The locale to update if applicable.")]
        public string Locale { get; set; } = null!;

        [CommandOption("-a|--apply")]
        [Description("Apply and publish all the required edits. The default behaviour is to only list the detected changes.")]
        public bool Apply { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (settings.Fields.Length != settings.Values.Length)
        {
            return ValidationResult.Error($"Mismatch in field ({settings.Fields.Length}) and value ({settings.Values.Length}) count.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        _console.WriteNormalWithHighlights($"Reading info on content type '{settings.ContentType}'...", Globals.StyleHeading);

        var contentType = await ContentfulManagementClient.GetContentType(settings.ContentType);

        var matchedFields = contentType.Fields
            .Select(f => f.Id)
            .Intersect(settings.Fields)
            .ToHashSet();

        var missingFields = settings.Fields
            .Where(f => !matchedFields.Contains(f))
            .ToArray();

        if (missingFields.Length > 0)
        {
            throw new CliException($"The field(s) named '{string.Join(',', missingFields)}' not in content type '{settings.ContentType}'");
        }

        _console.WriteNormalWithHighlights($"Reading locales...", Globals.StyleHeading);

        var locales = await ContentfulManagementClient.GetLocalesCollection();

        var defaultLocale = locales
            .First(l => l.Default)
            .Code;

        settings.Locale ??= defaultLocale;

        if (!locales.Any(l => l.Code != default))
        {
            throw new CliException($"The locale '{defaultLocale}' was not found in the Contentful space");
        }

        var scriptObject = CreateScriptObject();
        var compiledTemplates = settings.Values.Select(v => Template.Parse(v)).ToArray();

        _console.WriteNormalWithHighlights($"Reading management API entries for '{settings.ContentType}'...", Globals.StyleHeading);

        var fullEntries = ContentfulEntryEnumerator.Entries<Entry<JObject>>(ContentfulManagementClient, settings.ContentType)
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            .ToDictionary(e => e.SystemProperties.Id, e => e);

        var entriesToUpdate = new List<Entry<JObject>>();

        _console.WriteNormalWithHighlights($"Reading delivery API entries for '{settings.ContentType}'...", Globals.StyleHeading);

        await foreach (var (entry, _) in ContentfulEntryEnumerator.DeliveryEntries<JObject>(ContentfulClient, settings.ContentType, contentType.DisplayField))
        {
            scriptObject.SetValue(settings.ContentType, entry, true);

            var fieldChanged = new bool[settings.Fields.Length];

            for (var i = 0; i < settings.Fields.Length; i++)
            {
                var fieldName = settings.Fields[i];

                var fieldValue = string.IsNullOrEmpty(settings.Values[i])
                    ? null
                    : compiledTemplates[i].Render(scriptObject, memberRenamer: member => member.Name.ToCamelCase());

                var oldValue = entry[fieldName]?.ToString();

                if (oldValue != fieldValue)
                {
                    if (entry.ContainsKey(fieldName)) entry.Remove(fieldName);
                    entry.Add(fieldName, fieldValue);
                    fieldChanged[i] = true;
                }
            }

            if (fieldChanged.Any(v => v))
            {
                var id = entry["$id"]?.Value<string>();

                if (id == null) continue;

                _console.WriteNormalWithHighlights($"'{settings.ContentType}' with id '{id}' will be updated", Globals.StyleHeading);

                var entryForUpdate = fullEntries[id];

                if (entryForUpdate.Fields is not JObject objForUpdate) continue;

                for (var i = 0; i < settings.Fields.Length; i++)
                {
                    if (fieldChanged[i])
                    {
                        var fieldName = settings.Fields[i];

                        var fieldToUpdate = objForUpdate[fieldName];

                        if (fieldToUpdate is null)
                        {
                            fieldToUpdate = new JObject();
                            fieldToUpdate[settings.Locale] = null;
                            objForUpdate.Add(fieldName, fieldToUpdate);
                        }

                        fieldToUpdate[settings.Locale] = entry[fieldName];

                        _console.WriteNormalWithHighlights($"... field '{fieldName}' will be updated with '{entry[fieldName]}'", Globals.StyleHeading);
                    }
                }

                entriesToUpdate.Add(entryForUpdate);
            }

            scriptObject.Remove(settings.ContentType);
        }

        if (settings.Apply)
        {
            await _bulkActionExecutor
                .WithContentType(settings.ContentType)
                .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                .WithNewEntries(entriesToUpdate)
                .WithConcurrentTaskLimit(5)
                .WithMillisecondsBetweenCalls(250)
                .Execute(BulkAction.Upsert);
        }

        return 0;
    }

    private ScriptObject CreateScriptObject()
    {
        ScriptObject? scriptObject = [];

        CuteFunctions.ContentfulManagementClient = ContentfulManagementClient;

        CuteFunctions.ContentfulClient = ContentfulClient;

        scriptObject.SetValue("cute", new CuteFunctions(), true);

        return scriptObject;
    }
}