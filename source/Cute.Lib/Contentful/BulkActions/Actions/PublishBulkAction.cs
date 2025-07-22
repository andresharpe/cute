namespace Cute.Lib.Contentful.BulkActions.Actions;

public class PublishBulkAction(ContentfulConnection contentfulConnection, HttpClient httpClient)
    : BulkActionBase(contentfulConnection, httpClient)
{
    public override IList<ActionProgressIndicator> ActionProgressIndicators() =>
    [
        new() { Intent = "Getting entries..." },
        new() { Intent = "Publishing entries..." },
    ];

    public override async Task<IEnumerable<string>> ExecuteAsync(Action<BulkActionProgressEvent>[]? progressUpdaters = null)
    {
        await GetWithEntries(progressUpdaters?[0]);

        if (_applyChanges)
        {
            await PublishWithEntries(progressUpdaters?[1]);
        }
        else
        {
            NotifyUserInterface($"Skipping publish step. Omit --no-publish to skip this step.", progressUpdaters?[1]);
        }

        return _withEntries!.Select(e => e.Sys.Id);
    }
}