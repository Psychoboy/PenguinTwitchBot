using System.Net.WebSockets;
using System.Text;
using PenguinTwitchBot.TwitchApi.EventSub.Websockets.Client;

namespace PenguinTwitchBot.TwitchApi.EventSub.Websockets.Extensions
{
    internal static partial class LogExtensions
    {
        const LogLevel LogMessageLogLevel = LogLevel.Debug;
        [LoggerMessage(LogLevel.Error, "Websocket reconnect for SessionId {sessionId} failed!")]
        public static partial void LogReconnectFailed(this ILogger<EventSubWebsocketClient> logger, string sessionId);
        [LoggerMessage("{closeStatus} - {closeStatusDescription}")]
        public static partial void LogWebsocketClosed(this ILogger<WebsocketClient> logger, LogLevel logLevel, WebSocketCloseStatus closeStatus, string closeStatusDescription);
        [LoggerMessage(LogMessageLogLevel, "{message}")]
        public static partial void LogMessage(this ILogger logger, string message);
        [LoggerMessage(LogLevel.Warning, "Found unknown message type: {messageType}")]
        public static partial void LogUnknownMessageType(this ILogger<EventSubWebsocketClient> logger, string messageType);
        [LoggerMessage(LogLevel.Warning, "Websocket reconnect for SessionId {sessionId} requested!")]
        public static partial void LogReconnectRequested(this ILogger<EventSubWebsocketClient> logger, string sessionId);
        [LoggerMessage(LogLevel.Critical, "Websocket {sessionId} disconnected at {disconnectedAt}. Reason: {disconnectReason}")]
        public static partial void LogForceDisconnected(this ILogger<EventSubWebsocketClient> logger, string sessionId, DateTime? disconnectedAt, string disconnectReason);
        

        public static void LogMessage(this ILogger<EventSubWebsocketClient> logger, byte[] message)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                __LogMessageCallback(logger, Encoding.UTF8.GetString(message), null);
            }
        }

    }
}