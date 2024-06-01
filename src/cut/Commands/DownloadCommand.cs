using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Contentful.Core.Search;
using Cut.Exceptions;
using Cut.OutputAdapters;
using Cut.Services;
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
    private HtmlRenderer _htmlRenderer = new();

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

        await AnsiConsole.Progress()
            .HideCompleted(false)
            .AutoRefresh(true)
            .AutoClear(true)
            .Columns(
                [
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn(),
                ]
            )

            .StartAsync(async ctx =>
            {
                var taskPrepare = ctx.AddTask("Preparing download");
                var taskExtract = ctx.AddTask($"Downloading {settings.ContentType} entries");

                var totalRows = 0;
                var increment = 0d;
                var skip = 0;
                var page = 100;

                using var outputAdapter = OutputAdapterFactory.Create(settings.Format, settings.ContentType);

                var contentInfo = await _contentfulClient.GetContentType(settings.ContentType);
                taskPrepare.Increment(40);

                var locales = await _contentfulClient.GetLocalesCollection();
                taskPrepare.Increment(40);

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

                var taskSaving = ctx.AddTask("Saving results");

                outputAdapter.Save();

                taskSaving.Increment(100);
                taskSaving.StopTask();

                _console.WriteNormal($"{totalRows} {settings.ContentType} entries downloaded to {outputAdapter.FileName}");
            });

        return 0;
    }

    private static JToken? ToDisplayValue(Entry<JObject> entry, string fieldName, string locale = "en")
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
        dataTable.Columns.Add("sys.version");

        foreach (var field in contentInfo.Fields)
        {
            var newFields = new List<string>();
            var suffix = field.Type.Equals("Array") ? "[]" : "";

            if (field.Type.Equals("Location"))
            {
                newFields.Add($"{field.Id}.lat{suffix}");
                newFields.Add($"{field.Id}.lon{suffix}");
            }
            else
            {
                newFields.Add($"{field.Id}{suffix}");
            }

            if (field.Localized)
            {
                foreach (var fieldName in newFields)
                {
                    foreach (var locale in locales)
                    {
                        dataTable.Columns.Add($"{locale.Code}.{fieldName}", ToNativeType(field.Type));
                    }
                }
            }
            else
            {
                foreach (var fieldName in newFields)
                {
                    dataTable.Columns.Add($"{fieldName}", ToNativeType(field.Type));
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
                await SetFieldValue(entry, dataRow, field);
            }
        }

        return dataRow;
    }

    private async Task SetFieldValue(Entry<JObject> entry, DataRow dataRow, Field field, string locale = "en")
    {
        var value = ToDisplayValue(entry, field.Id, locale);
        var fieldPrefix = field.Localized ? locale + "." : string.Empty;

        if (value == null)
        {
            if (field.Type.Equals("Location"))
            {
                dataRow[fieldPrefix + field.Id + ".lat"] = DBNull.Value;
                dataRow[fieldPrefix + field.Id + ".lon"] = DBNull.Value;
            }
            else if (field.Type.Equals("Array"))
            {
                dataRow[fieldPrefix + field.Id + "[]"] = DBNull.Value;
            }
            else
            {
                dataRow[fieldPrefix + field.Id] = DBNull.Value;
            }
            return;
        }

        if (field.Type.Equals("Location"))
        {
            var latLong = (JObject)value;
            dataRow[fieldPrefix + field.Id + ".lat"] = latLong["lat"];
            dataRow[fieldPrefix + field.Id + ".lon"] = latLong["lon"];
        }
        else if (field.Type.Equals("Link"))
        {
            dataRow[fieldPrefix + field.Id] = ((JObject)value)["sys"]?.Value<string>("id");
        }
        else if (field.Type.Equals("Object"))
        {
            dataRow[fieldPrefix + field.Id] = JsonConvert.SerializeObject(value);
        }
        else if (field.Type.Equals("Array"))
        {
            dataRow[fieldPrefix + field.Id + "[]"] = string.Join('|', value.Select(e => e["sys"]!["id"]!.Value<string>()));
        }
        else
        {
            dataRow[fieldPrefix + field.Id] = await ToValue(value, field.Type);
        }
    }

    private static Type ToNativeType(string type)
    {
        return type switch
        {
            "Symbol" => typeof(string),
            "RichText" => typeof(string),
            "Integer" => typeof(int),
            "Location" => typeof(double),
            "Link" => typeof(string),
            "Object" => typeof(string),
            "Array" => typeof(string),
            "Boolean" => typeof(bool),
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
            "Symbol" => value,
            "RichText" => await UnHtml(value),
            "Integer" => value,
            "Location" => value,
            "Link" => value,
            "Object" => value,
            "Array" => value,
            "Boolean" => value,
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

        if (nodeType.Equals("document"))
        {
            var content = data["content"];

            return new Document
            {
                Data = new() { Target = null },
                NodeType = nodeType,
                Content = content!.Select(ConvertToDocument!).Cast<IContent>().ToList(),
            };
        }
        else if (nodeType.Equals("paragraph"))
        {
            var content = data["content"];

            return new Contentful.Core.Models.Paragraph
            {
                Data = new() { Target = null },
                NodeType = nodeType,
                Content = content!.Select(ConvertToDocument!).Cast<IContent>().ToList(),
            };
        }
        else if (nodeType.Equals("text"))
        {
            return new Contentful.Core.Models.Text
            {
                Data = new() { Target = null },
                NodeType = nodeType,
                Value = data.Value<string>("value"),
            };
        }
        else
        {
            throw new CliException($"No IContent conversion for '{data["nodeType"]}'.");
        }
    }
}