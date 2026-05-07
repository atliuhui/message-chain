namespace Mcf;

/// <summary>
/// Drives a generic attempt loop: invokes <c>action</c> repeatedly until it
/// reports success or the configured retry budget is exhausted, waiting between
/// failed attempts according to a delay schedule.
/// <para>
/// The policy itself is unaware of the underlying step / record / status; it
/// only sees the boolean outcome reported by <c>action</c> (<c>true</c> = stop,
/// <c>false</c> = retry if budget allows).
/// </para>
/// </summary>
sealed class RetryPolicy
{
    readonly int max_retries;
    readonly IReadOnlyList<TimeSpan> retry_delays;

    /// <param name="maxRetries">
    /// Maximum number of <b>retry</b> attempts (not counting the first
    /// attempt). <c>0</c> means no retry.
    /// </param>
    /// <param name="retryDelays">
    /// Wait times before each retry attempt: the k-th retry waits
    /// <c>delays[k-1]</c>; if k exceeds the list length the last entry is
    /// repeated; an empty list means no wait. <c>null</c> is treated as empty.
    /// </param>
    public RetryPolicy(int maxRetries, IReadOnlyList<TimeSpan>? retryDelays)
    {
        if (maxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries), maxRetries, "Retry count must be non-negative.");
        }
        max_retries = maxRetries;
        retry_delays = retryDelays ?? Array.Empty<TimeSpan>();
    }
    /// <summary>
    /// Repeatedly invokes <paramref name="action"/> until it returns
    /// <c>true</c> (success) or the retry budget is exhausted. Returns the
    /// total number of times <paramref name="action"/> was invoked
    /// (<c>≥ 1</c>).
    /// </summary>
    public async Task<int> ExecuteAsync(Func<Task<bool>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var attempts = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;
            var success = await action().ConfigureAwait(false);
            if (success)
            {
                return attempts;
            }
            if (attempts > max_retries)
            {
                return attempts;
            }

            var delay = ResolveDelay(attempts);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    TimeSpan ResolveDelay(int attempt)
    {
        if (retry_delays.Count == 0)
        {
            return TimeSpan.Zero;
        }
        // The k-th retry uses delays[k-1]; clamp to the last entry when k
        // exceeds the list length. After the n-th attempt, the next retry is
        // retry #n, so k = attempt.
        var index = Math.Min(attempt, retry_delays.Count) - 1;
        return retry_delays[index];
    }
}
