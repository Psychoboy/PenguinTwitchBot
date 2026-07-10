using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http.Connections.Client;
using PenguinTwitchBot.Bot.Hubs;

namespace PenguinTwitchBot.Helpers;

/// <summary>
/// Helper class for managing SignalR connections with consistent error handling across components.
/// </summary>
public static class SignalRConnectionHelper
{
    /// <summary>
    /// Resolves the URL used by server-side SignalR clients when connecting back to this app.
    /// Prefers a configured internal base URL or the HTTP Kestrel endpoint to avoid requiring
    /// a trusted HTTPS certificate for in-process self-connections.
    /// </summary>
    public static Uri ResolveHubUri(IConfiguration appConfiguration, NavigationManager navigation, string hubPath)
    {
        ArgumentNullException.ThrowIfNull(appConfiguration);
        ArgumentNullException.ThrowIfNull(navigation);

        var normalizedHubPath = hubPath.StartsWith('/') ? hubPath : $"/{hubPath}";

        var configuredBaseUrl = appConfiguration["SignalR:InternalBaseUrl"];
        if (TryCreateBaseUri(configuredBaseUrl, out var internalBaseUri))
        {
            return new Uri(internalBaseUri, normalizedHubPath);
        }

        var httpEndpointUrl = appConfiguration["Kestrel:Endpoints:Http:Url"];
        if (TryCreateBaseUri(httpEndpointUrl, out var httpEndpointBaseUri))
        {
            return new Uri(httpEndpointBaseUri, normalizedHubPath);
        }

        return navigation.ToAbsoluteUri(normalizedHubPath);
    }

    private static bool TryCreateBaseUri(string? url, out Uri baseUri)
    {
        baseUri = default!;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsedUri))
        {
            return false;
        }

        if (parsedUri.Host == "0.0.0.0" || parsedUri.Host == "[::]" || parsedUri.Host == "::")
        {
            var port = parsedUri.IsDefaultPort ? string.Empty : $":{parsedUri.Port}";
            parsedUri = new Uri($"{parsedUri.Scheme}://localhost{port}");
        }

        baseUri = new Uri(parsedUri.ToString().TrimEnd('/') + "/", UriKind.Absolute);
        return true;
    }

    /// <summary>
    /// Starts a SignalR hub connection with consistent exception handling for expected disconnection scenarios.
    /// </summary>
    /// <param name="hubConnection">The hub connection to start.</param>
    /// <param name="logger">Logger instance for tracking connection issues.</param>
    /// <param name="snackbar">Optional snackbar service to display user-facing warnings.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>True if connection started successfully, false if an expected exception occurred.</returns>
    public static async Task<bool> StartWithExpectedExceptionHandlingAsync(
        this HubConnection hubConnection,
        ILogger logger,
        ISnackbar? snackbar = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await hubConnection.StartAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            // Expected during disposal or user navigation
            logger.LogDebug("SignalR connection was canceled (expected during disposal or navigation)");
            return false;
        }
        catch (Microsoft.AspNetCore.SignalR.HubException ex) when (ex.Message.Contains("Handshake was canceled"))
        {
            // Expected when user navigates away before connection completes
            logger.LogDebug("SignalR handshake was canceled (expected during navigation)");
            return false;
        }
        catch (System.IO.IOException ex) when (ex.InnerException is SocketException socketEx && 
                                               socketEx.SocketErrorCode == SocketError.OperationAborted)
        {
            // SocketError.OperationAborted: I/O operation aborted due to thread exit or app request
            logger.LogDebug("SignalR connection aborted (expected during disposal)");
            return false;
        }
        catch (Exception ex) when (IsExpectedCanceledSignalRStartException(ex))
        {
            logger.LogDebug(ex, "SignalR connection startup was canceled (expected during disposal or navigation)");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to establish SignalR connection");
            snackbar?.Add("Unable to establish live connection. Data may be delayed.", Severity.Warning);
            return false;
        }
    }

    public static async Task<bool> StartWithExpectedExceptionHandlingAsync(
        this ISignalRHubConnection hubConnection,
        ILogger logger,
        ISnackbar? snackbar = null)
    {
        try
        {
            await hubConnection.StartAsync();
            return true;
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("SignalR connection was canceled (expected during disposal or navigation)");
            return false;
        }
        catch (Microsoft.AspNetCore.SignalR.HubException ex) when (ex.Message.Contains("Handshake was canceled"))
        {
            logger.LogDebug("SignalR handshake was canceled (expected during navigation)");
            return false;
        }
        catch (System.IO.IOException ex) when (ex.InnerException is SocketException socketEx &&
                                               socketEx.SocketErrorCode == SocketError.OperationAborted)
        {
            logger.LogDebug("SignalR connection aborted (expected during disposal)");
            return false;
        }
        catch (Exception ex) when (IsExpectedCanceledSignalRStartException(ex))
        {
            logger.LogDebug(ex, "SignalR connection startup was canceled (expected during disposal or navigation)");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to establish SignalR connection");
            snackbar?.Add("Unable to establish live connection. Data may be delayed.", Severity.Warning);
            return false;
        }
    }

    private static bool IsExpectedCanceledSignalRStartException(Exception exception)
    {
        var allExceptions = (exception as AggregateException)?.Flatten().InnerExceptions ?? [exception];
        var sawException = false;

        foreach (var innerException in allExceptions)
        {
            sawException = true;

            if (innerException is OperationCanceledException || innerException is TaskCanceledException)
            {
                continue;
            }

            if (innerException is TransportFailedException transportFailedException &&
                ContainsOnlyCancellationExceptions(transportFailedException))
            {
                continue;
            }

            return false;
        }

        return sawException;
    }

    private static bool ContainsOnlyCancellationExceptions(Exception exception)
    {
        var allExceptions = (exception as AggregateException)?.Flatten().InnerExceptions ?? [exception];

        foreach (var innerException in allExceptions)
        {
            if (ReferenceEquals(innerException, exception))
            {
                if (innerException is OperationCanceledException || innerException is TaskCanceledException)
                {
                    continue;
                }

                if (innerException.InnerException is not null && ContainsOnlyCancellationExceptions(innerException.InnerException))
                {
                    continue;
                }

                return false;
            }

            if (innerException is not OperationCanceledException && innerException is not TaskCanceledException)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gracefully disposes a SignalR hub connection, stopping it first if still connected.
    /// </summary>
    /// <param name="hubConnection">The hub connection to dispose.</param>
    /// <param name="logger">Logger instance for tracking disposal issues.</param>
    public static async Task DisposeGracefullyAsync(this HubConnection? hubConnection, ILogger logger)
    {
        if (hubConnection is null) return;

        try
        {
            // Stop the connection gracefully before disposing
            if (hubConnection.State != HubConnectionState.Disconnected)
            {
                await hubConnection.StopAsync();
            }
            await hubConnection.DisposeAsync();
        }
        catch (Exception ex)
        {
            // Log but don't throw - disposal should be silent to user
            logger.LogDebug(ex, "Exception during SignalR connection disposal (non-critical)");
        }
    }
}
