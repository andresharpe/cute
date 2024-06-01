using Contentful.Core;
using Contentful.Core.Extensions;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Contentful.Core.Search;
using Cut.Constants;
using Cut.Exceptions;
using Cut.InputAdapters;
using Cut.OutputAdapters;
using Cut.Services;
using Cut.UiComponents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Dynamic;

namespace Cut.Commands;

public class UploadCommand : LoggedInCommand<UploadCommand.Settings>
{
    private string _defaultLocale = null!;

    public UploadCommand(IConsoleWriter console, IPersistedTokenCache tokenCache)
        : base(console, tokenCache)
    { }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--content-type")]
        [Description("Specifies the content type to download data for")]
        public string ContentType { get; set; } = null!;

        [CommandOption("-p|--path")]
        [Description("The local path to the file containg the data to sync")]
        public string Path { get; set; } = default!;

        [CommandOption("-f|--format")]
        [Description("The format of the file specified in '--path' (Excel/Csv/Tsv/Json/Yaml)")]
        public InputFileFormat? Format { get; set; } = null!;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (!System.IO.File.Exists(settings.Path))
        {
            return ValidationResult.Error($"Path not found - {settings.Path}");
        }

        if (settings.Format == null)
        {
            var ext = new FileInfo(settings.Path).Extension.ToLowerInvariant();

            settings.Format = ext switch
            {
                ".xlsx" => InputFileFormat.Excel,
                ".csv" => InputFileFormat.Csv,
                ".tsv" => InputFileFormat.Tsv,
                ".json" => InputFileFormat.Json,
                ".yaml" => InputFileFormat.Yaml,
                ".yml" => InputFileFormat.Yaml,
                _ => throw new CliException($"Could not determine the format for {settings.Path}. Use the --format switch to specify the file format.")
            };
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        if (result != 0 || _contentfulClient == null || settings.Format == null) return result;

        await ProgressBars.Instance().StartAsync(async ctx =>
        {
            var taskPrepare = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.Alien} Initializing[/]");
            var taskExtractLocal = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.House} Reading file[/]");
            var taskExtractCloud = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.SatelliteAntenna} Downloading[/]");
            var taskMatchEntries = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.CoupleWithHeart} Matching[/]");

            // Get info about content
            var contentInfo = await _contentfulClient.GetContentType(settings.ContentType);
            taskPrepare.Increment(50);

            // Get locale info
            var locales = await _contentfulClient.GetLocalesCollection();
            _defaultLocale = locales.First().Code;
            taskPrepare.Increment(50);
            taskPrepare.StopTask();

            // Get entries from local file
            using var inputAdapter = InputAdapterFactory.Create(settings.Format.Value, settings.ContentType);
            taskExtractLocal.MaxValue = inputAdapter.GetRecordCount();
            var localEntries = inputAdapter.GetRecords((o, i) => taskExtractLocal.Increment(1));
            taskExtractLocal.StopTask();

            // Get cloud entries (Contentful)
            var cloudEntries = await GetContentfulEntries(_contentfulClient, settings.ContentType, contentInfo.DisplayField, taskExtractCloud);
            taskExtractCloud.StopTask();

            // Match 'em
            var indexedLocalEntries = localEntries.ToDictionary(o => GetValueWithKeys(o, "sys", "id")?.ToString() ?? ContentfulIdGenerator.NewId(), o => o);
            var indexedCloudEntries = cloudEntries.ToDictionary(o => o.SystemProperties.Id, o => o);
            var matched = 0;
            var cloudNewer = 0;
            var localNewer = 0;
            var valuesDiffer = 0;
            var uploaded = 0;
            taskMatchEntries.MaxValue = indexedLocalEntries.Count;
            foreach (var (localKey, localValue) in indexedLocalEntries)
            {
                var newEntry = ToValidContentfulObject(localValue, contentInfo);

                if (indexedCloudEntries.TryGetValue(localKey, out var cloudEntry))
                {
                    if (newEntry.SystemProperties.Version < cloudEntry.SystemProperties.Version)
                    {
                        cloudNewer++;
                    }
                    else if (newEntry.SystemProperties.Version > cloudEntry.SystemProperties.Version)
                    {
                        localNewer++;
                    }
                    else if (ValuesDiffer(newEntry, cloudEntry))
                    {
                        valuesDiffer++;
                    }
                    else
                    {
                        matched++;
                    }
                }
                else
                {
                    // var newCloudEntry = await _contentfulClient.CreateOrUpdateEntry(newEntry, null, settings.ContentType, 0);
                    // await _contentfulClient.PublishEntry(newCloudEntry.SystemProperties.Id, newCloudEntry.SystemProperties.Version!.Value);
                    uploaded++;
                }
                taskMatchEntries.Increment(1);
            }

            _console.WriteSubHeading($"{taskExtractLocal.MaxValue:N0} {settings.ContentType} entries read from {inputAdapter.FileName}");
            _console.WriteSubHeading($"{taskExtractCloud.MaxValue:N0} {settings.ContentType} entries downloaded from Contentful space");
            _console.WriteSubHeading($"{matched:N0} local entries with matching cloud entries");
            _console.WriteSubHeading($"{cloudNewer:N0} cloud entries newer than local entries");
            _console.WriteSubHeading($"{localNewer:N0} local entries newer than cloud entries");
            _console.WriteSubHeading($"{uploaded:N0} new local entry(ies) uploaded to the cloud");
        });

        return 0;
    }

    private bool ValuesDiffer(Entry<dynamic> newEntry, Entry<JObject> cloudEntry)
    {
        var versionLocal = newEntry.SystemProperties.Version;
        var versionCloud = cloudEntry.SystemProperties.Version;

        return versionLocal != versionCloud;
    }

    private Entry<dynamic> ToValidContentfulObject(IDictionary<string, object?> localValue, ContentType contentType)
    {
        Entry<dynamic> result = new Entry<dynamic>();

        dynamic expandoFields = new ExpandoObject();

        string? id = null;
        int? version = null;

        if (localValue.TryGetValue("sys", out var sysValue) && sysValue is IDictionary<string, object?> sys)
        {
            if (sys.TryGetValue("id", out var idValue) && idValue is string idString)
                id = idString;

            if (sys.TryGetValue("version", out var versionValue) && versionValue is double versionInt)
                version = (int)versionInt;
        }

        result.Fields = expandoFields;
        result.Metadata = new() { Tags = [] };
        result.SystemProperties = new SystemProperties()
        {
            Id = id ?? ContentfulIdGenerator.NewId(),
            Version = version ?? 0
        };

        var fields = (IDictionary<string, object?>)expandoFields;

        foreach (var (key, value) in localValue)
        {
            if (key == "sys") continue; // strip this out

            if (value is null) continue;

            var fld = contentType.Fields.First(f => f.Id == key);

            var fldType = fld.Type;

            var localeValues = (IDictionary<string, object?>)value;

            switch (fldType)
            {
                case "Symbol":
                    fields.Add(key, localeValues.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString()));
                    break;

                case "RichText":
                    fields.Add(key, localeValues.ToDictionary(kv => kv.Key, kv => ToDocument(kv.Value)));
                    break;

                case "Integer":
                    fields.Add(key, localeValues.ToDictionary(kv => kv.Key, kv => Convert.ToInt32(kv.Value)));
                    break;

                case "Number":
                    fields.Add(key, localeValues.ToDictionary(kv => kv.Key, kv => Convert.ToDouble(kv.Value)));
                    break;

                case "Boolean":
                    fields.Add(key, localeValues.ToDictionary(kv => kv.Key, kv => Convert.ToBoolean(kv.Value)));
                    break;

                case "Date":
                    fields.Add(key, localeValues.ToDictionary(kv => kv.Key, kv => Convert.ToDateTime(kv.Value)));
                    break;

                case "Location":
                    fields.Add(key, localeValues.ToDictionary(kv => kv.Key, kv => ToLocation(kv.Value)));
                    break;

                case "Link":
                    var linkEntries = localeValues.Where(kv => kv.Value is not null);
                    if (linkEntries.Any())
                    {
                        fields.Add(key, linkEntries.ToDictionary(kv => kv.Key, kv => ToLink(kv.Value, fld.LinkType)));
                    }
                    break;

                case "Object": // untested
                    fields.Add(key, localeValues.ToDictionary(kv => kv.Key, kv => new
                    {
                        type = "Link",
                        linkType = fld.LinkType,
                        id = kv.Value?.ToString()
                    }));
                    break;

                case "Array":

                    fields.Add(key, localeValues.ToDictionary(kv => kv.Key, kv => ToLinkArray(kv.Value)));
                    break;

                default:
                    throw new CliException($"No handler for converting '{key}' to Contentful type '{fldType}'");
            }
        }

        return result;
    }

    private object ToLink(object? value, string linkType)
    {
        return new
        {
            sys = new
            {
                type = "Link",
                linkType,
                id = value?.ToString()
            }
        };
    }

    private object? ToLinkArray(object? value)
    {
        if (value == null) return null;

        var arrayValue = (string[])value;
        var links = new List<object>();

        foreach (var id in arrayValue)
        {
            links.Add(new
            {
                sys = new
                {
                    type = "Link",
                    linkType = "Entry",
                    id,
                }
            });
        }
        return links.ToArray();
    }

    private static Location? ToLocation(object? fldValue)
    {
        if (fldValue == null) return null;

        var location = (IDictionary<string, object?>)fldValue;

        if (location is null) return null;

        return new Location()
        {
            Lat = (double)location["lat"]!,
            Lon = (double)location["lon"]!
        };
    }

    private Document? ToDocument(object? fldValue)
    {
        if (fldValue == null) return null;

        var text = fldValue.ToString();

        return new Document()
        {
            NodeType = "document",
            Data = new(),
            Content = [
                new Contentful.Core.Models.Paragraph()
                {
                    NodeType = "paragraph",
                    Data = new(),
                    Content = [
                        new Contentful.Core.Models.Text()
                        {
                            NodeType = "text",
                            Data = new(),
                            Marks = [],
                            Value = text
                        }]
                }
            ],
        };
    }

    private static object? GetValueWithKeys(IDictionary<string, object?> obj, params string[] keys)
    {
        if (keys.Length == 0) return obj;

        if (obj.TryGetValue(keys[0], out object? value))
        {
            if (value is IDictionary<string, object?> dict)
            {
                return GetValueWithKeys(dict, keys[1..]);
            }
            else
            {
                return value;
            }
        }
        return null;
    }

    private static async Task<IEnumerable<Entry<JObject>>> GetContentfulEntries(ContentfulManagementClient client,
        string contentType,
        string sortOrder,
        ProgressTask taskExtractCloud)
    {
        List<Entry<JObject>> result = [];

        var skip = 0;
        var page = 100;
        taskExtractCloud.MaxValue = 1;

        while (true)
        {
            var query = new QueryBuilder<Dictionary<string, object?>>()
                .ContentTypeIs(contentType)
                .Skip(skip)
                .Limit(page)
                .OrderBy($"fields.{sortOrder}")
                .Build();

            var entries = await client.GetEntriesCollection<Entry<JObject>>(query);

            if (!entries.Any()) break;

            if (taskExtractCloud.MaxValue == 1)
            {
                taskExtractCloud.MaxValue = entries.Total;
            }

            foreach (var entry in entries)
            {
                result.Add(entry);
                taskExtractCloud.Increment(1);
            }

            skip += page;
        }

        return result;
    }
}