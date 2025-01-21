using Contentful.Core.Models;
using Cute.Lib.Exceptions;
using Newtonsoft.Json.Linq;

namespace Cute.Lib.Contentful.BulkActions.Actions
{
    public class ClearFieldsBulkAction(ContentfulConnection contentfulConnection, HttpClient httpClient, List<string> fields, string? key) : UpsertBulkAction(contentfulConnection, httpClient)
    {
        private readonly List<string> _fields = fields;
        private readonly string? _key = key;
        public override IList<ActionProgressIndicator> ActionProgressIndicators() =>
        [
            new() { Intent = "Getting Contentful entries and clearing fields..." },
            new() { Intent = "Upserting entries..." },
        ];

        public override async Task ExecuteAsync(Action<BulkActionProgressEvent>[]? progressUpdaters = null)
        {
            await GetAllEntriesForComparison(progressUpdaters?[0]);
            await UpsertRequiredEntries(_withUpdatedFlatEntries!, progressUpdaters?[1]);
        }

        private async Task GetAllEntriesForComparison(Action<BulkActionProgressEvent>? progressUpdater)
        {
            _ = _contentType ?? throw new CliException("You need to call 'WithContentType' before 'Execute'");

            _ = _contentTypeId ?? throw new CliException("You need to call 'WithContentType' before 'Execute'");

            _ = _contentLocales ?? throw new CliException("You need to call 'WithContentLocales' before 'Execute'");

            _withUpdatedFlatEntries = [];

            var steps = -1;

            var currentStep = 1;

            var queryBuilder = new EntryQuery.Builder()
                    .WithContentType(_contentType)
                    .WithPageSize(1)
                    .WithLocale("*")
                    .WithIncludeLevels(0);

            if (!string.IsNullOrEmpty(_key))
            {
                queryBuilder.WithQueryString($"fields.key={_key}");
            }

            await foreach (var (entry, total) in
                _contentfulConnection.GetManagementEntries<Entry<JObject>>(queryBuilder.Build()))
            {
                if (steps == -1)
                {
                    steps = total;
                }
                var cleared = false;
                foreach (var fieldName in _fields)
                {
                    foreach (var contentLocale in _contentLocales.Locales)
                    {
                        if (contentLocale == _contentLocales.DefaultLocale)
                        {
                            continue;
                        }

                        if (entry.Fields[fieldName]?[contentLocale] != null)
                        {
                            entry.Fields[fieldName]![contentLocale]!.Parent!.Remove();
                            cleared = true;
                        }

                    }
                }

                progressUpdater?.Invoke(new(currentStep++, steps, null, null));

                if (cleared)
                {
                    _withUpdatedFlatEntries.Add(entry);
                }
            }

            progressUpdater?.Invoke(new(currentStep, steps, $"Cleared {_withUpdatedFlatEntries.Count} entries for '{_contentTypeId}'.", null));
        }
    }
}
