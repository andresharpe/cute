
using Spectre.Console.Cli;
using Cut.Services;
using Spectre.Console;
using Cut.Constants;
using Contentful.Core.Models;
using Contentful.Core.Search;
using System.Data;
using System.ComponentModel;
using System.Dynamic;
using Newtonsoft.Json.Linq;

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

        var table = new Spectre.Console.Table()
            .RoundedBorder()
            .Title(settings.ContentType)
            .BorderColor(Globals.StyleDim.Foreground);

        table.AddColumn("Content Id");
        table.AddColumn(contentInfo.DisplayField);

        await AnsiConsole.Live(table)
            .StartAsync(async ctx => {

                var skip = 0;
                var page = 100;

                while (true)
                {
                    var query = new QueryBuilder<dynamic>()
                        .ContentTypeIs(settings.ContentType)
                        .Skip(skip)
                        .Limit(page)
                        .Build();

                    var entries = await _contentfulClient.GetEntriesCollection<Entry<ExpandoObject>>(query);

                    if (entries.Count() == 0) break;

                    foreach (var entry in entries)
                    {
                        IDictionary<string, object?> fields = entry.Fields;

                        var displayField = fields[contentInfo.DisplayField];
                        
                        IDictionary<string, object?> displayFieldValue = (ExpandoObject)displayField!;
                        
                        var displayValue = displayFieldValue["en"]?.ToString() ?? string.Empty;
                        
                        var values = new List<string>
                        {
                            entry.SystemProperties.Id,
                            displayValue
                        };
                        
                        table.AddRow(values.ToArray());
                        ctx.Refresh();
                    }

                    skip += page;

                }

            });

        return 0;
    }

}