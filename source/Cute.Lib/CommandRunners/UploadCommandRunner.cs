using Contentful.Core;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Lib.Contentful;
using Cute.Lib.Enums;
using Cute.Lib.InputAdapters;
using Cute.Lib.Serializers;
using Newtonsoft.Json.Linq;

namespace Cute.Lib.CommandRunners;

public class UploadCommandRunner
{
    private string _contentType = string.Empty;

    private bool _applyChanges = false;

    private ContentfulManagementClient _contentfulManagementClient = null!;

    private ContentType _contentInfo = null!;

    private ContentfulCollection<Locale> _locales = null!;

    private InputFileFormat _fileFormat = InputFileFormat.Excel;

    private IEnumerable<IDictionary<string, object?>> _localEntries = null!;

    private readonly List<Entry<JObject>> _contentfulEntries = [];

    private string _localFileName = string.Empty;

    private string? _filePath;

    protected UploadCommandRunner()
    {
    }

    public class Builder
    {
        private readonly UploadCommandRunner _runner;

        public Builder()
        {
            _runner = new UploadCommandRunner();
        }

        public Builder ForContentType(string contentType)
        {
            _runner._contentType = contentType;
            return this;
        }

        public Builder ApplyChanges(bool applyChanges)
        {
            _runner._applyChanges = applyChanges;
            return this;
        }

        public Builder WithContentfulManagementClient(ContentfulManagementClient contentfulManagementClient)
        {
            _runner._contentfulManagementClient = contentfulManagementClient;
            return this;
        }

        public Builder WithFileFormat(InputFileFormat fileFormat)
        {
            _runner._fileFormat = fileFormat;
            return this;
        }

        public Builder WithFilePath(string filePath)
        {
            _runner._filePath = filePath;
            return this;
        }

        public UploadCommandRunner Build()
        {
            return _runner;
        }
    }

    public async Task<CommandRunnerResult> LoadContentType(Action<int, int> progressUpdater)
    {
        var steps = 2;
        var currentStep = 1;

        _contentInfo = await _contentfulManagementClient.GetContentType(_contentType);
        progressUpdater(currentStep++, steps);

        _locales = await _contentfulManagementClient.GetLocalesCollection();
        progressUpdater(currentStep++, steps);

        return new CommandRunnerResult(RunnerResult.Success);
    }

    public Task<CommandRunnerResult> LoadLocalEntries(Action<int, int> progressUpdater)
    {
        using var inputAdapter = InputAdapterFactory.Create(_fileFormat, _contentType, _filePath);

        var steps = inputAdapter.GetRecordCount();

        _localEntries = inputAdapter.GetRecords((o, i) =>
        {
            progressUpdater(i, steps);
        });

        _localFileName = inputAdapter.FileName;

        return Task.FromResult(new CommandRunnerResult(RunnerResult.Success));
    }

    public Task<CommandRunnerResult> LoadContentfulEntries(Action<int, int> progressUpdater)
    {
        var steps = -1;
        var currentStep = 1;

        foreach (var (entry, entries) in EntryEnumerator.Entries(_contentfulManagementClient, _contentType, _contentInfo.DisplayField))
        {
            _contentfulEntries.Add(entry);

            if (steps == -1)
            {
                steps = entries.Total;
            }

            progressUpdater(currentStep++, steps);
        }
        return Task.FromResult(new CommandRunnerResult(RunnerResult.Success));
    }

    public async Task<UploadCommandRunnerResult> CompareLocalAndContentfulEntries(Action<int, int> progressUpdater)
    {
        var serializer = new EntrySerializer(_contentInfo, _locales.Items);
        var indexedLocalEntries = _localEntries.ToDictionary(o => o["sys.Id"]?.ToString() ?? ContentfulIdGenerator.NewId(), o => o);
        var indexedCloudEntries = _contentfulEntries.ToDictionary(o => o.SystemProperties.Id, o => o);
        var matchedEntries = 0;
        var updatedCloudEntries = 0;
        var updatedLocalEntries = 0;
        var newLocalEntries = 0;
        var mismatchedValues = 0;
        var changesApplied = 0;

        var steps = indexedLocalEntries.Count;
        var currentStep = 1;

        foreach (var (localKey, localValue) in indexedLocalEntries)
        {
            var newEntry = serializer.DeserializeEntry(localValue);

            if (indexedCloudEntries.TryGetValue(localKey, out var cloudEntry))
            {
                if (newEntry.SystemProperties.Version < cloudEntry.SystemProperties.Version)
                {
                    updatedCloudEntries++;
                }
                else if (newEntry.SystemProperties.Version > cloudEntry.SystemProperties.Version)
                {
                    updatedLocalEntries++;
                }
                else if (ValuesDiffer(newEntry, cloudEntry))
                {
                    mismatchedValues++;
                }
                else
                {
                    matchedEntries++;
                }
            }
            else
            {
                if (_applyChanges)
                {
                    // _console.WriteNormal($"Creating and uploading {settings.ContentType} {localKey}...");

                    var newCloudEntry = await _contentfulManagementClient.CreateOrUpdateEntry<JObject>(
                        newEntry.Fields,
                        id: localKey,
                        version: 1,
                        contentTypeId: _contentType);

                    await _contentfulManagementClient.PublishEntry(localKey, 1);

                    changesApplied++;
                }

                newLocalEntries++;
            }

            progressUpdater(currentStep++, steps);
        }

        var result = new UploadCommandRunnerResult(RunnerResult.Success);
        result.MatchedEntries = matchedEntries;
        result.UpdatedCloudEntries = updatedCloudEntries;
        result.UpdatedLocalEntries = updatedLocalEntries;
        result.NewLocalEntries = newLocalEntries;
        result.MismatchedValues = mismatchedValues;
        result.ChangesApplied = changesApplied;
        result.InputFilename = _localFileName;

        return result;
    }

    private static bool ValuesDiffer(Entry<JObject> newEntry, Entry<JObject> cloudEntry)
    {
        var versionLocal = newEntry.SystemProperties.Version;
        var versionCloud = cloudEntry.SystemProperties.Version;

        return versionLocal != versionCloud;
    }
}

public class UploadCommandRunnerResult(RunnerResult result, string? message = null) : CommandRunnerResult(result, message)
{
    public int MatchedEntries { get; internal set; }
    public int UpdatedLocalEntries { get; internal set; }
    public int UpdatedCloudEntries { get; internal set; }
    public int NewLocalEntries { get; internal set; }
    public int MismatchedValues { get; internal set; }
    public int ChangesApplied { get; internal set; }
    public string InputFilename { get; internal set; } = string.Empty;
}