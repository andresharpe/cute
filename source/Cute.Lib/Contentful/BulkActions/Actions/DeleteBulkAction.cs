using Cute.Lib.Contentful.BulkActions.Models;
using Cute.Lib.RateLimiters;

namespace Cute.Lib.Contentful.BulkActions.Actions;

public class DeleteBulkAction(ContentfulConnection contentfulConnection, HttpClient httpClient)
    : BulkActionBase(contentfulConnection, httpClient)
{
    public override IList<ActionProgressIndicator> ActionProgressIndicators() =>
    [
        new() { Intent = "Getting entries..." },
        new() { Intent = "Unpublishing entries..." },
        new() { Intent = "Deleting entries..." },
    ];

    public override async Task ExecuteAsync(Action<BulkActionProgressEvent>[]? progressUpdaters = null)
    {
        await GetWithEntries(progressUpdaters?[0]);

        await UnPublishWithEntries(progressUpdaters?[1]);

        await DeleteWithEntries(progressUpdaters?[2]);
    }

    protected async Task DeleteWithEntries(Action<BulkActionProgressEvent>? progressUpdater)
    {
        _ = _withEntries ?? throw new InvalidOperationException("Entries must be loaded before publishing.");

        var count = _withEntries.Count;

        progressUpdater?.Invoke(new(0, count, $"Deleting {count} entries...", null));

        await DeleteRequiredEntries(_withEntries, progressUpdater);

        count = Math.Max(count, 1);

        progressUpdater?.Invoke(new(count, count, $"Deleted {_withEntries.Count} entries.", null));
    }

    private async Task DeleteRequiredEntries(List<BulkItem> allEntries,
      Action<BulkActionProgressEvent>? progressUpdater)
    {
        var tasks = new Task[_concurrentTaskLimit];
        var taskNo = 0;
        var totalCount = allEntries.Count;
        var processed = 0;

        await Task.Delay(1);

        foreach (var item in allEntries)
        {
            var itemId = item.Sys.Id;
            var itemVersion = item.Sys.Version ?? 0;
            var displayFieldValue = item.Sys.DisplayFieldValue;

            processed++;

            var messageProcessed = processed;

            FormattableString message = $"...deleting '{_contentTypeId}' item '{itemId}' ({messageProcessed}/{totalCount}) '{displayFieldValue}'";

            tasks[taskNo++] = RateLimiter.SendRequestAsync(
                    () => _contentfulConnection.ManagementClient.DeleteEntry(itemId, itemVersion),
                    message,
                    (m) => NotifyUserInterface(m, progressUpdater),
                    (e) => NotifyUserInterfaceOfError(e, progressUpdater)
                );

            if (taskNo >= tasks.Length)
            {
                Task.WaitAll(tasks);
                taskNo = 0;
            }

            progressUpdater?.Invoke(new(processed, totalCount, null, null));
        }

        Task.WaitAll(tasks.Where(t => t is not null).ToArray());

        progressUpdater?.Invoke(new(processed, totalCount, null, null));
    }
}