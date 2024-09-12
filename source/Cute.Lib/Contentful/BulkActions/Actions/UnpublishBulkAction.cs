using Cute.Lib.Contentful.BulkActions.Models;

namespace Cute.Lib.Contentful.BulkActions.Actions;

public class UnpublishBulkAction(ContentfulConnection contentfulConnection, HttpClient httpClient)
    : BulkActionBase(contentfulConnection, httpClient)
{
    public static BulkAction BulkAction => BulkAction.Unpublish;

    public override IList<ActionProgressIndicator> ActionProgressIndicators() =>
    [
        new() { Intent = "Getting entries..." },
        new() { Intent = "Unpublishing entries..." },
        new() { Intent = "Publishing..." },
    ];

    public override async Task ExecuteAsync(Action<BulkActionProgressEvent>[]? progressUpdaters = null)
    {
        await GetWithEntries(progressUpdaters?[0]);

        await UnPublishWithEntries(progressUpdaters?[1]);
    }
}