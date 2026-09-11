using System.Net.WebSockets;

namespace PenguinTwitchBot.Bot.WebSocketEvents
{
    public sealed record WsSocketStatus(Guid Id, string DisplayName, WebSocketState State, int SentMessages, int DroppedMessages, int PeakPendingMessages);

    public sealed record WsEventHandlerStatus(
        int GlobalQueueDepth,
        int GlobalQueueCapacity,
        int PerSocketQueueCapacity,
        IReadOnlyList<WsSocketStatus> Connections);

    public interface IWsEventHandler
    {
        Task AddToQueue(WsEvent evt);
        Task<WsEventHandlerStatus> GetStatusAsync();
        Task ApplySettingsAsync(int maxQueueSize, int perSocketQueueCapacity);
        void ClearGlobalQueue();
        Task<bool> ReconnectSocketAsync(Guid id);
        Task<int> ReconnectAllSocketsAsync();
        Task CloseAllSockets();
        Task Handle(Guid id, WebSocket webSocket, string? displayName = null);
        void Pause();
        void Resume();
    }
}