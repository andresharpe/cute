using Cute.Lib.Exceptions;
using System.Diagnostics;

namespace Cute.Lib.RateLimiters;

public static class RateLimiter
{
    private const int _requestsPerBatch = 7;

    private const int _timeSpanPerBatchInSeconds = 1;

    private const int _retryLimit = 10;

    private static readonly SemaphoreSlim _semaphore = new(_requestsPerBatch, _requestsPerBatch);

    private static readonly TimeSpan _timeSpanPerBatch = TimeSpan.FromSeconds(_timeSpanPerBatchInSeconds);

    private static int _requestsSentInBatch = 0;

    private static DateTime _nextBatchTime = DateTime.UtcNow.Add(_timeSpanPerBatch);

    private static readonly object _lockObj = new();

    private static readonly object _nullObj = new();

    public static async Task SendRequestAsync(Func<Task> mainAction,
        FormattableString? actionMessage = null,
        Action<FormattableString>? actionNotifier = null,
        Action<FormattableString>? errorNotifier = null)
    {
        await SendRequestAsync(
            async () =>
            {
                await mainAction.Invoke();
                return _nullObj;
            },
            actionMessage, actionNotifier, errorNotifier
        );
    }

    public static async Task<T> SendRequestAsync<T>(Func<Task<T>> mainAction,
        FormattableString? actionMessage = null,
        Action<FormattableString>? actionNotifier = null,
        Action<FormattableString>? errorNotifier = null)
    {
        var retryAttempt = 0;

        while (true)
        {
            try
            {
                await _semaphore.WaitAsync(); // Limit concurrent requests

                try
                {
                    await ThrottleAsync();

                    actionNotifier?.Invoke(actionMessage ?? $"Starting...");

                    return await mainAction();
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                if (retryAttempt > _retryLimit)
                {
                    errorNotifier?.Invoke($"Too many retries. {ex.Message}.");
                    throw new CliException($"Too many retries. {ex.Message}",ex);
                }

                retryAttempt++;

                errorNotifier?.Invoke($"{ex.Message} (Retry={retryAttempt})");

                if (errorNotifier is null)
                {
                    Debug.WriteLine($"{ex.Message} (Retry={retryAttempt})");
                }
                await Task.Delay(1000 / _requestsPerBatch);
            }
        }
    }

    private static async Task ThrottleAsync()
    {
        lock (_lockObj)
        {
            if (_requestsSentInBatch >= _requestsPerBatch)
            {
                var now = DateTime.UtcNow;

                if (now < _nextBatchTime)
                {
                    var delayTime = _nextBatchTime - now;

                    // Debug.WriteLine($"Throttling for {delayTime.TotalMilliseconds}ms");

                    Task.Delay(delayTime).Wait();
                }

                _nextBatchTime = DateTime.UtcNow.Add(_timeSpanPerBatch);

                _requestsSentInBatch = 0;
            }
            _requestsSentInBatch++;
        }

        await Task.Delay(1000 / _requestsPerBatch);
    }
}