using Contentful.Core.Models;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.BulkActions.Actions
{
    public interface IBulkAction
    {
        IList<ActionProgressIndicator> ActionProgressIndicators();

        Task<IEnumerable<string>> ExecuteAsync(Action<BulkActionProgressEvent>[]? progressUpdaters = null);

        BulkActionBase WithApplyChanges(bool applyChanges);

        BulkActionBase WithBulkActionCallLimit(int bulkActionCallLimit);

        BulkActionBase WithConcurrentTaskLimit(int concurrentTaskLimit);

        BulkActionBase WithContentfulConnection(ContentfulConnection contentfulConnection);

        BulkActionBase WithContentLocales(ContentLocales contentLocales);

        BulkActionBase WithContentType(ContentType contentType);

        BulkActionBase WithDisplayAction(Action<FormattableString> displayAction);

        BulkActionBase WithMillisecondsBetweenCalls(int millisecondsBetweenCalls);

        BulkActionBase WithPublishChunkSize(int publishChunkSize);

        BulkActionBase WithVerbosity(Verbosity verbosity);
    }
}