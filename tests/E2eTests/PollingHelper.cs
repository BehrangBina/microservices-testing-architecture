namespace E2eTests;

/// <summary>
/// Generic retry-with-timeout helper for asserting eventual consistency in
/// event-driven systems.
///
/// Polls a condition function every <paramref name="interval"/> until it returns true
/// or the <paramref name="timeout"/> is exceeded, then throws with a descriptive message.
/// </summary>
public static class PollingHelper
{
    /// <summary>
    /// Waits until <paramref name="condition"/> returns a non-null value, or throws after timeout.
    /// </summary>
    public static async Task<T> WaitForAsync<T>(
        Func<Task<T?>> condition,
        TimeSpan? timeout = null,
        TimeSpan? interval = null,
        string failMessage = "Condition was not met within the timeout.")
        where T : class
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        var poll = interval ?? TimeSpan.FromMilliseconds(500);

        while (DateTime.UtcNow < deadline)
        {
            var result = await condition();
            if (result is not null) return result;
            await Task.Delay(poll);
        }

        throw new TimeoutException(failMessage);
    }

    /// <summary>
    /// Waits until <paramref name="condition"/> returns true, or throws after timeout.
    /// </summary>
    public static async Task WaitForTrueAsync(
        Func<Task<bool>> condition,
        TimeSpan? timeout = null,
        TimeSpan? interval = null,
        string failMessage = "Condition was not met within the timeout.")
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        var poll = interval ?? TimeSpan.FromMilliseconds(500);

        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(poll);
        }

        throw new TimeoutException(failMessage);
    }
}
