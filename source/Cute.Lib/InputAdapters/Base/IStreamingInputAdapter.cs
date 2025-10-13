namespace Cute.Lib.InputAdapters.Base;

/// <summary>
/// Interface for streaming input adapters that process data in chunks to avoid memory issues with large datasets
/// </summary>
public interface IStreamingInputAdapter
{
    /// <summary>
    /// Gets the estimated total number of records that will be processed
    /// </summary>
    Task<int> GetEstimatedRecordCountAsync();

    /// <summary>
    /// Gets records in streaming fashion without loading everything into memory
    /// </summary>
    /// <param name="batchSize">Number of records to process in each batch (default: 1000)</param>
    /// <returns>Async enumerable of record batches</returns>
    IAsyncEnumerable<IEnumerable<IDictionary<string, object?>>> GetRecordBatchesAsync(int batchSize = 1000);

    /// <summary>
    /// Gets individual records in streaming fashion
    /// </summary>
    /// <returns>Async enumerable of individual records</returns>
    IAsyncEnumerable<IDictionary<string, object?>> GetRecordsStreamAsync();

    /// <summary>
    /// Source name for logging and display purposes
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Action notifier for progress updates
    /// </summary>
    Action<string>? ActionNotifier { get; set; }

    /// <summary>
    /// Error notifier for error handling
    /// </summary>
    Action<string>? ErrorNotifier { get; set; }

    /// <summary>
    /// Count progress notifier for batch processing progress
    /// </summary>
    Action<int, int, string?>? CountProgressNotifier { get; set; }
}