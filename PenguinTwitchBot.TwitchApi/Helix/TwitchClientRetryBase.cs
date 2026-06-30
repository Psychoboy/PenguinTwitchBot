using System.Text.Json;

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
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
            try
            {
                return await action();
            }
            catch (Exception ex) when (IsRetryable(ex))
            {
                if (attempt == MaxAttempts)
                {
                    Logger.LogError(ex, "Operation failed after retries: {operation}", operation);
                    throw;
                }

                var delay = TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1));
                Logger.LogWarning(ex, "Transient error during {operation} (attempt {attempt}/{maxAttempts}). Retrying in {delayMs} ms.", 
                    operation, attempt, MaxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Non-transient error during {operation}: {exceptionType}: {message} {failureHint}",
                    operation, ex.GetType().Name, ex.Message, GetFailureHint(ex));
                throw;
            }
#pragma warning restore S2139 // Exceptions should be either logged or rethrown but not both
        }

        throw new InvalidOperationException($"Retry loop exited unexpectedly for operation '{operation}'.");
    }

    /// <summary>
    /// Executes a void operation with exponential backoff retry for transient errors.
    /// </summary>
    protected Task ExecuteWithRetryAsync(Func<Task> action, string operation)
    {
        
        return ExecuteWithRetryAsync(
            async () =>
            {
                await action();
                return true;
            },
            operation);

    }

    private static bool IsRetryable(Exception ex)
    {
        return ex switch
        {
            TaskCanceledException => true,
            TimeoutException => true,
            HttpRequestException {StatusCode: null} => true, // Network errors without HTTP response
            HttpRequestException {StatusCode: System.Net.HttpStatusCode.TooManyRequests} => true, // Rate limiting
            HttpRequestException httpEx when (int)(httpEx.StatusCode ?? 0) >= 500 => true, // Server errors
            _ => false
        };
    }

    private static string GetFailureHint(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpRequestException when httpRequestException.StatusCode is not null =>
                $"(HTTP {(int)httpRequestException.StatusCode.Value} {httpRequestException.StatusCode.Value})",
            HttpRequestException => "(HTTP request failed)",
            JsonException => "(JSON parsing failed)",
            _ => string.Empty
        };
    }
}
