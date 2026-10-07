using System.Net.WebSockets;
using PenguinTwitchBot.Bot.WebSocketEvents;

namespace PenguinTwitchBot.Bot.Notifications
{
    public sealed record WebSocketClientStatus(
        Guid Id,
        string DisplayName,
        WebSocketState State,
        int SentMessages,
        int DroppedMessages,
        int PeakPendingMessages,
        IReadOnlyList<string> Topics);

    public sealed record WebSocketMessengerStatus(
        int GlobalQueueDepth,
        int GlobalQueueCapacity,
        int PerSocketQueueCapacity,
        IReadOnlyList<WebSocketClientStatus> Connections);

    public interface IWebSocketMessenger
    {
        /// <summary>Broadcasts to every connected client regardless of subscriptions.</summary>
        Task AddToQueue(string message);

        /// <summary>Sends only to clients subscribed to <paramref name="topic"/>.</summary>
        Task AddToQueue(string topic, string message);

        /// <summary>Sends a structured event to clients subscribed to the <see cref="WsTopics.Events"/> topic.</summary>
        Task AddToQueue(WsEvent evt);

        Task Handle(Guid id, WebSocket webSocket, string? displayName = null, IEnumerable<string>? topics = null);
        Task<WebSocketMessengerStatus> GetStatusAsync();
        Task ApplySettingsAsync(int maxQueueSize, int perSocketQueueCapacity);
        void ClearGlobalQueue();
        Task<bool> ReconnectSocketAsync(Guid id);
        Task<int> ReconnectAllSocketsAsync();

        Task CloseAllSockets();
        void Pause();
        void Resume();
    }
}