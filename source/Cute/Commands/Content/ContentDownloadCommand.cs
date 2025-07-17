using Contentful.Core.Models;
using Cute.Commands.BaseCommands;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Lib.OutputAdapters;
using Cute.Lib.Serializers;
using Cute.Services;
using Cute.UiComponents;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using static Cute.Commands.Content.ContentDownloadCommand;

namespace Cute.Commands.Content;

public class ContentDownloadCommand(IConsoleWriter console, ILogger<ContentDownloadCommand> logger,
         AppSettings appSettings)
    : BaseLoggedInCommand<Settings>(console, logger, appSettings)
{
    public class Settings : ContentCommandSettings
    {
        [CommandOption("-f|--format <FORMAT>")]
        [Description("The output format for the download operation (Excel/CSV/TSV/JSON/YAML)")]
        public OutputFileFormat? Format { get; set; }

        [CommandOption("-p|--path <PATH>")]
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

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        settings.ContentTypeId = await ResolveContentTypeId(settings.ContentTypeId) ??
            throw new CliException("You need to specify a content type to download.");

        var contentType = await GetContentTypeOrThrowError(settings.ContentTypeId);

        settings.Path ??= settings.ContentTypeId + settings.Format switch
        {
            OutputFileFormat.Excel => ".xlsx",
            OutputFileFormat.Csv => ".csv",
            OutputFileFormat.Tsv => ".tsv",
            OutputFileFormat.Json => ".json",
            OutputFileFormat.Yaml => ".yaml",
            _ => throw new NotImplementedException(),
        };

        await ProgressBars.Instance()
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                var taskPrepare = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.Alien} Initializing[/]");
                var taskExtract = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.SatelliteAntenna} Downloading[/]");

                using var outputAdapter = OutputAdapterFactory.Create(settings.Format!.Value, settings.ContentTypeId, settings.Path);

                taskPrepare.Increment(80);

                var serializer = new EntrySerializer(contentType, await ContentfulConnection.GetContentLocalesAsync());

                outputAdapter.AddHeadings(serializer.ColumnFieldNames);
                taskPrepare.Increment(20);
                taskPrepare.StopTask();

                taskExtract.MaxValue = 1;

                var enumerable = ContentfulConnection.GetManagementEntries<Entry<JObject>>(contentType);

                await foreach (var (entry, total) in enumerable)
                {
                    if (taskExtract.MaxValue == 1)
                    {
                        taskExtract.MaxValue = total;
                    }
                    outputAdapter.AddRow(serializer.SerializeEntry(entry), entry.SystemProperties.GetEntryState());
                    taskExtract.Increment(1);
                }

                taskExtract.StopTask();

                var taskSaving = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.Rocket} Saving[/]");

                outputAdapter.Save();

                taskSaving.Increment(100);
                taskSaving.StopTask();

                _console.WriteSubHeading($"{taskExtract.MaxValue:N0} {settings.ContentTypeId} entries downloaded to {outputAdapter.FileSource}");
            });

        return 0;
    }
}