namespace Cute.Lib.Contentful.BulkActions.Actions;

public class PublishBulkAction(ContentfulConnection contentfulConnection, HttpClient httpClient)
    : BulkActionBase(contentfulConnection, httpClient)
{
    public override IList<ActionProgressIndicator> ActionProgressIndicators() =>
    [
        new() { Intent = "Getting entries..." },
        new() { Intent = "Publishing entries..." },
    ];

    public override async Task ExecuteAsync(Action<BulkActionProgressEvent>[]? progressUpdaters = null)
    {
        await GetWithEntries(progressUpdaters?[0]);

        await PublishWithEntries(progressUpdaters?[1]);
    }
}