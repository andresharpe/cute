using Contentful.Core.Errors;
using Cute.Lib.Exceptions;
using System.Diagnostics;

namespace Cute.Lib.RateLimiters;

public class RateLimiter
{
    private readonly int _requestsPerBatch;

    private readonly int _timeSpanPerBatchInSeconds;

    private readonly int _retryLimit;

    private readonly SemaphoreSlim _semaphore;

    private readonly TimeSpan _timeSpanPerBatch;

    private int _requestsSentInBatch;

    private DateTime _nextBatchTime;

    private readonly object _lockObj = new();

    private readonly object _nullObj = new();

    public RateLimiter(int requestsPerBatch = 7,
        int timeSpanPerBatchInSeconds = 1,
        int retryLimit = 10)
    {
        _requestsPerBatch = requestsPerBatch;

        _timeSpanPerBatchInSeconds = timeSpanPerBatchInSeconds;

        _retryLimit = retryLimit;

        _semaphore = new(_requestsPerBatch, _requestsPerBatch);

        _timeSpanPerBatch = TimeSpan.FromSeconds(_timeSpanPerBatchInSeconds);

        _nextBatchTime = DateTime.UtcNow.Add(_timeSpanPerBatch);

        _requestsSentInBatch = 0;
    }

    public async Task SendRequestAsync(Func<Task> mainAction,
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

    public async Task<T> SendRequestAsync<T>(Func<Task<T>> mainAction,
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
                if (ex is ContentfulException ce)
                {
                    if (ce.Message.StartsWith("Response size too big. Maximum allowed response size:"))
                    {
                        throw new CliException($"{ex.Message}", ex);
                    }
                }

                if (retryAttempt > _retryLimit)
                {
                    errorNotifier?.Invoke($"Too many retries. {ex.Message}.");
                    throw new CliException($"Too many retries. {ex.Message}", ex);
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

    private async Task ThrottleAsync()
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