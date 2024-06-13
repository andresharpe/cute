using Cute.Constants;
using Cute.Lib.Exceptions;
using Cute.Lib.CommandRunners;
using Cute.Lib.Enums;
using Cute.Services;
using Cute.UiComponents;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands;

public class UploadCommand : LoggedInCommand<UploadCommand.Settings>
{
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

        [CommandOption("-a|--apply")]
        [Description("Apply and publish all the calculated changes. The default behaviour is to only list the detected changes.")]
        public bool Apply { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (!File.Exists(settings.Path))
        {
            return ValidationResult.Error($"Path not found - {settings.Path}");
        }

        if (settings.Format == null)
        {
            var ext = new FileInfo(settings.Path).Extension.ToLowerInvariant();

            settings.Format ??= ext switch
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

            var runner = new UploadCommandRunner.Builder()
                .WithContentfulManagementClient(_contentfulClient)
                .WithFilePath(settings.Path)
                .WithFileFormat(settings.Format.Value)
                .ForContentType(settings.ContentType)
                .ApplyChanges(settings.Apply)
                .Build();

            CommandRunnerResult result;

            // Get info about content

            result = await runner.LoadContentType((step, steps) =>
            {
                taskPrepare.MaxValue = steps;
                taskPrepare.Value = step;
            });

            if (result.Result == RunnerResult.Error)
            {
                throw new CliException(result.Message);
            }

            taskPrepare.StopTask();

            // Get entries from local file

            result = await runner.LoadLocalEntries((step, steps) =>
            {
                taskExtractLocal.MaxValue = steps;
                taskExtractLocal.Value = step;
            });

            if (result.Result == RunnerResult.Error)
            {
                throw new CliException(result.Message);
            }

            taskExtractLocal.StopTask();

            // Get cloud entries (Contentful)

            result = await runner.LoadContentfulEntries((step, steps) =>
            {
                taskExtractCloud.MaxValue = steps;
                taskExtractCloud.Value = step;
            });

            if (result.Result == RunnerResult.Error)
            {
                throw new CliException(result.Message);
            }

            taskExtractCloud.StopTask();

            // Match 'em

            var matchResult = await runner.CompareLocalAndContentfulEntries((step, steps) =>
            {
                taskMatchEntries.MaxValue = steps;
                taskMatchEntries.Value = step;
            });

            if (matchResult.Result == RunnerResult.Error)
            {
                throw new CliException(result.Message);
            }

            taskMatchEntries.StopTask();

            _console.WriteSubHeading($"{taskExtractLocal.MaxValue:N0} {settings.ContentType} entries read from {matchResult.InputFilename}");
            _console.WriteSubHeading($"{taskExtractCloud.MaxValue:N0} {settings.ContentType} entries downloaded from Contentful space");
            _console.WriteSubHeading($"{matchResult.MatchedEntries:N0} local entries with matching cloud entries (id & version)");
            _console.WriteSubHeading($"{matchResult.UpdatedCloudEntries:N0} cloud entries newer than local entries (id & version)");
            _console.WriteSubHeading($"{matchResult.UpdatedLocalEntries:N0} local entries newer than cloud entries (id & version)");
            _console.WriteSubHeading($"{matchResult.MismatchedValues:N0} entries with mismatched values (all fields)");
            _console.WriteSubHeading($"{matchResult.ChangesApplied:N0} changes applied to Contentful.");
            _console.WriteSubHeading($"{0:N0} new local entry(ies) uploaded to the cloud");
        });

        return 0;
    }
}