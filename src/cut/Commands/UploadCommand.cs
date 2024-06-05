using Contentful.Core;
using Contentful.Core.Models;
using Cut.Constants;
using Cut.Exceptions;
using Cut.InputAdapters;
using Cut.Lib.Contentful;
using Cut.Lib.Serializers;
using Cut.Services;
using Cut.UiComponents;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cut.Commands;

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
            taskPrepare.Increment(50);
            taskPrepare.StopTask();

            // Get entries from local file
            using var inputAdapter = InputAdapterFactory.Create(settings.Format.Value, settings.ContentType);
            taskExtractLocal.MaxValue = inputAdapter.GetRecordCount();
            var localEntries = inputAdapter.GetRecords((o, i) => taskExtractLocal.Increment(1));
            taskExtractLocal.StopTask();

            // Get cloud entries (Contentful)
            var cloudEntries = GetContentfulEntries(settings.ContentType, contentInfo.DisplayField, taskExtractCloud);
            taskExtractCloud.StopTask();

            // Match 'em
            var serializer = new EntrySerializer(contentInfo, locales.Items);
            var indexedLocalEntries = localEntries.ToDictionary(o => o["sys.Id"]?.ToString() ?? ContentfulIdGenerator.NewId(), o => o);
            var indexedCloudEntries = cloudEntries.ToDictionary(o => o.SystemProperties.Id, o => o);
            var matched = 0;
            var cloudNewer = 0;
            var localNewer = 0;
            var valuesDiffer = 0;
            var uploaded = 0;

            taskMatchEntries.MaxValue = indexedLocalEntries.Count;

            foreach (var (localKey, localValue) in indexedLocalEntries)
            {
                var newEntry = serializer.DeserializeEntry(localValue);

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
                    if (settings.Apply)
                    {
                        _console.WriteAlert("Applying changes to Contentful.");

                        var newCloudEntry = await _contentfulClient.CreateOrUpdateEntry(newEntry, id: null, contentTypeId: settings.ContentType, version: 0);

                        await _contentfulClient.PublishEntry(newCloudEntry.SystemProperties.Id, newCloudEntry.SystemProperties.Version!.Value);
                    }
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

    private static bool ValuesDiffer(Entry<JObject> newEntry, Entry<JObject> cloudEntry)
    {
        var versionLocal = newEntry.SystemProperties.Version;
        var versionCloud = cloudEntry.SystemProperties.Version;

        return versionLocal != versionCloud;
    }
}