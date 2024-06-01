using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Contentful.Core.Search;
using Cut.Constants;
using Cut.Exceptions;
using Cut.OutputAdapters;
using Cut.Services;
using Cut.UiComponents;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Data;
using System.Net;

namespace Cut.Commands;

public class DownloadCommand : LoggedInCommand<DownloadCommand.Settings>
{
    private readonly HtmlRenderer _htmlRenderer = new();

    private string _defaultLocale = null!;

    public DownloadCommand(IConsoleWriter console, IPersistedTokenCache tokenCache)
        : base(console, tokenCache)
    { }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--content-type")]
        [Description("Specifies the content type to download data for")]
        public string ContentType { get; set; } = null!;

        [CommandOption("-f|--format")]
        [Description("The output format for the download operation (Excel/Csv/Tsv/Json/Yaml)")]
        public OutputFileFormat Format { get; set; } = OutputFileFormat.Excel;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        if (result != 0 || _contentfulClient == null) return result;

        await ProgressBars.Instance()
            .StartAsync(async ctx =>
            {
                var taskPrepare = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.Alien} Initializing[/]");
                var taskExtract = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.SatelliteAntenna} Downloading[/]");

                var totalRows = 0;
                var increment = 0d;
                var skip = 0;
                var page = 100;

                using var outputAdapter = OutputAdapterFactory.Create(settings.Format, settings.ContentType);

                var contentInfo = await _contentfulClient.GetContentType(settings.ContentType);
                taskPrepare.Increment(40);

                var locales = await _contentfulClient.GetLocalesCollection();
                taskPrepare.Increment(40);

                _defaultLocale = locales.First().Code;

                DataTable dataTable = ToDataTable(contentInfo, locales);

                outputAdapter.AddHeadings(dataTable);
                taskPrepare.Increment(20);
                taskPrepare.StopTask();

                while (true)
                {
                    var query = new QueryBuilder<Dictionary<string, object?>>()
                        .ContentTypeIs(settings.ContentType)
                        .Skip(skip)
                        .Limit(page)
                        .OrderBy($"fields.{contentInfo.DisplayField}")
                        .Build();

                    var entries = await _contentfulClient.GetEntriesCollection<Entry<JObject>>(query);

                    if (!entries.Any()) break;

                    if (increment == 0 && entries.Total > 0)
                    {
                        totalRows = entries.Total;
                        increment = (100d / entries.Total) * 100d;
                    }

                    foreach (var entry in entries)
                    {
                        DataRow row = await ToDataRow(dataTable, entry, contentInfo, locales);
                        outputAdapter.AddRow(row);
                    }

                    taskExtract.Increment(increment);

                    skip += page;
                }

                taskExtract.StopTask();

                var taskSaving = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.Rocket} Saving[/]");

                outputAdapter.Save();

                taskSaving.Increment(100);
                taskSaving.StopTask();

                _console.WriteSubHeading($"{totalRows:N0} {settings.ContentType} entries downloaded to {outputAdapter.FileName}");
            });

        return 0;
    }

    private static JToken? ToDisplayValue(Entry<JObject> entry, string fieldName, string locale)
    {
        if (!entry.Fields.TryGetValue(fieldName, out var selectedField))
        {
            return null;
        }

        if (selectedField.Contains(locale))
        {
            return null;
        }

        return selectedField[locale];
    }

    private DataTable ToDataTable(ContentType contentInfo, ContentfulCollection<Locale> locales)
    {
        var dataTable = new DataTable();

        dataTable.Columns.Add("sys.id");
        dataTable.Columns.Add("sys.version", typeof(int));

        foreach (var field in contentInfo.Fields)
        {
            var newFields = new List<(string preFix, string postFix)>();
            var arraySuffix = field.Type.Equals("Array") ? "[]" : string.Empty;

            if (field.Type.Equals("Location"))
            {
                newFields.Add((field.Id, ".lat"));
                newFields.Add((field.Id, ".lon"));
            }
            else
            {
                newFields.Add((field.Id, arraySuffix));
            }

            if (field.Localized)
            {
                foreach (var (preFix, postFix) in newFields)
                {
                    foreach (var locale in locales)
                    {
                        dataTable.Columns.Add($"{preFix}.{locale.Code}{postFix}", ToNativeType(field.Type));
                    }
                }
            }
            else
            {
                foreach (var (preFix, postFix) in newFields)
                {
                    dataTable.Columns.Add($"{preFix}.{_defaultLocale}{postFix}", ToNativeType(field.Type));
                }
            }
        }

        return dataTable;
    }

    private async Task<DataRow> ToDataRow(DataTable dataTable, Entry<JObject> entry, ContentType contentInfo, ContentfulCollection<Locale> locales)
    {
        var dataRow = dataTable.NewRow();

        dataRow["sys.id"] = entry.SystemProperties.Id;
        dataRow["sys.version"] = entry.SystemProperties.Version;

        foreach (var field in contentInfo.Fields)
        {
            if (field.Localized)
            {
                foreach (var locale in locales)
                {
                    await SetFieldValue(entry, dataRow, field, locale.Code);
                }
            }
            else
            {
                await SetFieldValue(entry, dataRow, field, _defaultLocale);
            }
        }

        return dataRow;
    }

    private async Task SetFieldValue(Entry<JObject> entry, DataRow dataRow, Field field, string locale)
    {
        var value = ToDisplayValue(entry, field.Id, locale);
        var fieldId = field.Id + "." + locale;

        if (value == null)
        {
            switch (field.Type)
            {
                case "Location":
                    dataRow[fieldId + ".lat"] = DBNull.Value;
                    dataRow[fieldId + ".lon"] = DBNull.Value;
                    break;

                case "Array":
                    dataRow[fieldId + "[]"] = DBNull.Value;
                    break;

                default:
                    dataRow[fieldId] = DBNull.Value;
                    break;
            }
            return;
        }

        switch (field.Type)
        {
            case "Location":
                {
                    var latLong = (JObject)value;
                    dataRow[fieldId + ".lat"] = latLong["lat"];
                    dataRow[fieldId + ".lon"] = latLong["lon"];
                    break;
                }

            case "Link":
                dataRow[fieldId] = ((JObject)value)["sys"]?.Value<string>("id");
                break;

            case "Object":
                dataRow[fieldId] = JsonConvert.SerializeObject(value);
                break;

            case "Array":
                {
                    dataRow[fieldId + "[]"] = string.Join('|', value.Select(e => e["sys"]!["id"]!.Value<string>()));
                    break;
                }

            default:
                dataRow[fieldId] = await ToValue(value, field.Type);
                break;
        }
    }

    private static Type ToNativeType(string type)
    {
        return type switch
        {
            "Symbol" => typeof(string),
            "RichText" => typeof(string),
            "Integer" => typeof(int),
            "Number" => typeof(double),
            "Boolean" => typeof(bool),
            "Date" => typeof(DateTime),
            "Location" => typeof(double),
            "Link" => typeof(string),
            "Object" => typeof(string),
            "Array" => typeof(string),
            _ => throw new CliException($"Contentful type '{type}' has no dotnet type conversion implemented."),
        };
    }

    private async Task<object> ToValue(JToken value, string type)
    {
        if (value == null)
        {
            return DBNull.Value;
        }

        return type switch
        {
            "Symbol" => value.Value<string>(),
            "RichText" => await UnHtml(value),
            "Integer" => value.Value<int>(),
            "Number" => value.Value<double>(),
            "Boolean" => value.Value<bool>(),
            "Date" => value.Value<DateTime>(),
            "Location" => value.Value<double>(),
            "Link" => value,
            "Object" => value,
            "Array" => value,

            _ => throw new CliException($"Contentful type '{type}' has no display value conversion implemented."),
        };
    }

    private async Task<string> UnHtml(JToken value)
    {
        var html = await _htmlRenderer.ToHtml((Document)ConvertToDocument(value)!);

        var htmlDoc = new HtmlDocument();

        htmlDoc.LoadHtml(html);

        return WebUtility.HtmlDecode(htmlDoc.DocumentNode.InnerText);
    }

    private object? ConvertToDocument(JToken data)
    {
        var nodeType = data["nodeType"]?.Value<string>() ?? "unknown";

        switch (nodeType)
        {
            case "document":
                {
                    var content = data["content"];

                    return new Document
                    {
                        Data = new() { Target = null },
                        NodeType = nodeType,
                        Content = content!.Select(ConvertToDocument!).Cast<IContent>().ToList(),
                    };
                }

            case "paragraph":
                {
                    var content = data["content"];

                    return new Contentful.Core.Models.Paragraph
                    {
                        Data = new() { Target = null },
                        NodeType = nodeType,
                        Content = content!.Select(ConvertToDocument!).Cast<IContent>().ToList(),
                    };
                }

            case "text":
                return new Contentful.Core.Models.Text
                {
                    Data = new() { Target = null },
                    NodeType = nodeType,
                    Value = data.Value<string>("value"),
                };

            default:
                throw new CliException($"No IContent conversion for '{data["nodeType"]}'.");
        }
    }
}