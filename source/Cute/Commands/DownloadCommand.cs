using Contentful.Core.Models;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.OutputAdapters;
using Cute.Lib.Serializers;
using Cute.Services;
using Cute.UiComponents;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands;

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

        [CommandOption("-f|--format")]
        [Description("The output format for the download operation (Excel/Csv/Tsv/Json/Yaml)")]
        public OutputFileFormat? Format { get; set; }

        [CommandOption("-p|--path")]
        [Description("The output path and filename for the download operation")]
        public string? Path { get; set; }
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (settings.Path is null && settings.Format is null)
        {
            settings.Format = OutputFileFormat.Excel;
        }
        else if (settings.Path is not null && settings.Format is null)
        {
            var ext = new FileInfo(settings.Path).Extension.ToLowerInvariant();

            settings.Format = ext switch
            {
                ".xlsx" => OutputFileFormat.Excel,
                ".csv" => OutputFileFormat.Csv,
                ".tsv" => OutputFileFormat.Tsv,
                ".json" => OutputFileFormat.Json,
                ".yaml" => OutputFileFormat.Yaml,
                ".yml" => OutputFileFormat.Yaml,
                _ => throw new CliException($"Could not determine the format for {settings.Path}. Use the --format switch to specify the file format.")
            };
        }

        settings.Path ??= settings.ContentType + settings.Format switch
        {
            OutputFileFormat.Excel => ".xlsx",
            OutputFileFormat.Csv => ".csv",
            OutputFileFormat.Tsv => ".tsv",
            OutputFileFormat.Json => ".json",
            OutputFileFormat.Yaml => ".yaml",
            _ => throw new NotImplementedException(),
        };

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        await ProgressBars.Instance()
            .StartAsync(async ctx =>
            {
                var taskPrepare = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.Alien} Initializing[/]");
                var taskExtract = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.SatelliteAntenna} Downloading[/]");

                using var outputAdapter = OutputAdapterFactory.Create(settings.Format!.Value, settings.ContentType, settings.Path);

                var contentInfo = await _contentfulManagementClient.GetContentType(settings.ContentType);
                taskPrepare.Increment(40);

                var locales = await _contentfulManagementClient.GetLocalesCollection();
                taskPrepare.Increment(40);

                var serializer = new EntrySerializer(contentInfo, locales.Items);

                outputAdapter.AddHeadings(serializer.ColumnFieldNames);
                taskPrepare.Increment(20);
                taskPrepare.StopTask();

                taskExtract.MaxValue = 1;

                await foreach (var (entry, entries) in ContentfulEntryEnumerator.Entries(_contentfulManagementClient, settings.ContentType, contentInfo.DisplayField))
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