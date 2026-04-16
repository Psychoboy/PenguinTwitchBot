using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Net.Sockets;

namespace DotNetTwitchBot.Helpers;

/// <summary>
/// Helper class for managing SignalR connections with consistent error handling across components.
/// </summary>
public static class SignalRConnectionHelper
{
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
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to establish SignalR connection");
            snackbar?.Add("Unable to establish live connection. Data may be delayed.", Severity.Warning);
            return false;
        }
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
