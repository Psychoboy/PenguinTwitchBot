using System.Net.WebSockets;

namespace PenguinTwitchBot.Bot.Notifications
{
    public sealed record WebSocketClientStatus(Guid Id, string DisplayName, WebSocketState State, int SentMessages, int DroppedMessages, int PeakPendingMessages);

    public interface IWebSocketMessenger
    {
        Task AddToQueue(string message);
        Task Handle(Guid id, WebSocket webSocket, string? displayName = null);
        Task<IReadOnlyList<WebSocketClientStatus>> GetStatusAsync();
        Task<bool> ReconnectSocketAsync(Guid id);

        Task CloseAllSockets();
        void Pause();
        void Resume();
    }
}