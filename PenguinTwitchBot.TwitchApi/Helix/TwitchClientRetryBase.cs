namespace PenguinTwitchBot.TwitchApi.Helix;

/// <summary>
/// Base class for Twitch API clients providing consistent exponential backoff retry logic.
/// </summary>
public abstract class TwitchClientRetryBase
{
    protected const int MaxAttempts = 3;
    protected readonly ILogger Logger;

    protected TwitchClientRetryBase(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Executes an operation with exponential backoff retry for transient errors.
    /// Non-transient errors throw immediately.
    /// </summary>
    protected async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, string operation)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < MaxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1));
                Logger.LogWarning(ex, "Transient error during {operation} (attempt {attempt}/{maxAttempts}). Retrying in {delayMs} ms.", 
                    operation, attempt, MaxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay);
            }
        }

        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Operation failed after retries: {operation}", operation);
            throw;
        }
    }

    /// <summary>
    /// Executes a void operation with exponential backoff retry for transient errors.
    /// </summary>
    protected async Task ExecuteWithRetryAsync(Func<Task> action, string operation)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < MaxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1));
                Logger.LogWarning(ex, "Transient error during {operation} (attempt {attempt}/{maxAttempts}). Retrying in {delayMs} ms.",
                    operation, attempt, MaxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay);
            }
        }

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Operation failed after retries: {operation}", operation);
            throw;
        }
    }

    /// <summary>
    /// Determines if an exception represents a transient error worth retrying.
    /// </summary>
    protected static bool IsTransient(Exception ex)
    {
        return ex is HttpRequestException
            || ex is TaskCanceledException
            || ex is TimeoutException;
    }
}
