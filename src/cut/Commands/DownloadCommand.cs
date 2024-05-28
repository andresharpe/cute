
using Spectre.Console.Cli;
using Cut.Services;
using Spectre.Console;
using Cut.Constants;
using Contentful.Core.Models;
using Contentful.Core.Search;
using System.ComponentModel;
using System.Dynamic;
using System.Data;
using Contentful.Core.Models.Management;
using Cut.Exceptions;
using ClosedXML.Excel;

namespace Cut.Commands;

public class DownloadCommand : LoggedInCommand<DownloadCommand.Settings>
{
    public DownloadCommand(IConsoleWriter console, IPersistedTokenCache tokenCache)
        : base(console, tokenCache)
    { }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--content-type")]
        [Description("Specifies the content type to download data for")]
        public string ContentType { get; set; } = null!;


    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {

        var result = await base.ExecuteAsync(context, settings);

        if (result != 0 || _contentfulClient == null) return result;

        var contentInfo = await _contentfulClient.GetContentType(settings.ContentType);

        var locales = await _contentfulClient.GetLocalesCollection();

        var table = new Spectre.Console.Table()
            .RoundedBorder()
            .Title(settings.ContentType)
            .BorderColor(Globals.StyleDim.Foreground);

        table.AddColumn("Content Id");
        table.AddColumn(contentInfo.DisplayField);

        DataTable dataTable = ToDataTable(contentInfo, locales);
        
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(settings.ContentType);
        var xlRow = 1; 
        var xlCol = 1;
        foreach (DataColumn col in dataTable.Columns)
        {
            sheet.Cell(xlRow, xlCol).Value = col.ColumnName;
            sheet.Cell(xlRow, xlCol).Style.Font.SetBold();
            sheet.Cell(xlRow, xlCol).Style.Fill.BackgroundColor = XLColor.LightCarminePink;
            xlCol++;
        }

        await AnsiConsole.Live(table)
            .StartAsync(async ctx =>
            {

                var skip = 0;
                var page = 100;

                while (true)
                {
                    var query = new QueryBuilder<dynamic>()
                        .ContentTypeIs(settings.ContentType)
                        .Skip(skip)
                        .Limit(page)
                        .OrderBy($"fields.{contentInfo.DisplayField}")
                        .Build();

                    var entries = await _contentfulClient.GetEntriesCollection<Entry<ExpandoObject>>(query);

                    if (!entries.Any()) break;

                    foreach (var entry in entries)
                    {
                        xlRow++;
                        xlCol = 1;
                        DataRow row = ToDataRow(dataTable, entry, contentInfo, locales);
                        foreach(var val in row.ItemArray) 
                        {
                            sheet.Cell(xlRow, xlCol++).Value = val?.ToString();
                        }

                        var displayValue = ToDisplayValue(entry, contentInfo.DisplayField)?.ToString() ?? string.Empty;
                        
                        table.AddRow(entry.SystemProperties.Id, displayValue);
                        
                        ctx.Refresh();
                    }

                    skip += page;

                }

            });

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(settings.ContentType + ".xlsx");

        return 0;
    }

    private DataRow ToDataRow(DataTable dataTable, Entry<ExpandoObject> entry, ContentType contentInfo, ContentfulCollection<Locale> locales)
    {
        var dataRow = dataTable.NewRow();

        dataRow["sys.id"] = entry.SystemProperties.Id;

        foreach (var field in contentInfo.Fields)
        {

          if (field.Type.Equals("Location"))
            {
                var nativeValue = ToDisplayValue(entry, field.Id);
                if (nativeValue is not null)
                {
                    var latLong = (IDictionary<string, object>)nativeValue;
                    dataRow[field.Id + ".lat"] = latLong["lat"];
                    dataRow[field.Id + ".lon"] = latLong["lon"];
                }
            }
            else if (field.Type.Equals("Link"))
            {
                // skip for now
            }
            else if (field.Type.Equals("Object"))
            {
                // skip for now
            }
            else if (field.Type.Equals("Array"))
            {
                // skip for now
            }
            else if (field.Localized)
            {
                foreach (var locale in locales)
                {
                    dataRow[$"{field.Id}.{locale.Code}"] = ToDisplayValue(entry, field.Id, locale.Code) ?? DBNull.Value;
                }
            }
            else
            {
                dataRow[field.Id] = ToDisplayValue(entry, field.Id) ?? DBNull.Value;
            }
        }

        dataTable.Rows.Add(dataRow);

        return dataRow;
    }


    private static object? ToDisplayValue(Entry<ExpandoObject> entry, string fieldName, string locale = "en")
    {
        IDictionary<string, object?> fields = entry.Fields;

        if (!fields.TryGetValue(fieldName, out var selectedField))
        {
            return null;
        }

        IDictionary<string, object?> selectedFieldValue = (ExpandoObject)selectedField!;

        if (!selectedFieldValue.TryGetValue(locale, out var displayValue))
        {
            return null;
        }
        
        return displayValue;
    }

    private DataTable ToDataTable(ContentType contentInfo, ContentfulCollection<Locale> locales)
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add("sys.id");
        foreach (var field in contentInfo.Fields)
        {
            var newFields = new List<string>();

            if (field.Type.Equals("Location"))
            {
                newFields.Add(field.Id + ".lat");
                newFields.Add(field.Id + ".lon");
            }
            else
            {
                newFields.Add(field.Id);
            }

            if (field.Localized)
            {
                foreach (var fieldName in newFields)
                {
                    foreach (var locale in locales)
                    {
                        dataTable.Columns.Add($"{fieldName}.{locale.Code}", ToNativeType(field.Type));
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

    private Type ToNativeType(string type)
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
}