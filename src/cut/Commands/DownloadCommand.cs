using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cut.Constants;
using Cut.Exceptions;
using Cut.Lib.Contentful;
using Cut.Lib.Serializers;
using Cut.OutputAdapters;
using Cut.Services;
using Cut.UiComponents;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Net;

namespace Cut.Commands;

public class DownloadCommand : LoggedInCommand<DownloadCommand.Settings>
{
    private readonly HtmlRenderer _htmlRenderer = new();

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

                using var outputAdapter = OutputAdapterFactory.Create(settings.Format, settings.ContentType);

                var contentInfo = await _contentfulClient.GetContentType(settings.ContentType);
                taskPrepare.Increment(40);

                var locales = await _contentfulClient.GetLocalesCollection();
                taskPrepare.Increment(40);

                var serializer = new EntrySerializer(contentInfo, locales.Items);

                outputAdapter.AddHeadings(serializer.ColumnFieldNames);
                taskPrepare.Increment(20);
                taskPrepare.StopTask();

                taskExtract.MaxValue = 1;

                foreach (var (entry, entries) in EntryEnumerator.Entries(_contentfulClient, settings.ContentType, contentInfo.DisplayField))
                {
                    if (taskExtract.MaxValue == 1)
                    {
                        taskExtract.MaxValue = entries.Total;
                    }
                    outputAdapter.AddRow(serializer.SerializeEntry(entry));
                    taskExtract.Increment(1);
                }

                taskExtract.StopTask();

                var taskSaving = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.Rocket} Saving[/]");

                outputAdapter.Save();

                taskSaving.Increment(100);
                taskSaving.StopTask();

                _console.WriteSubHeading($"{taskExtract.MaxValue:N0} {settings.ContentType} entries downloaded to {outputAdapter.FileName}");
            });

        return 0;
    }
}