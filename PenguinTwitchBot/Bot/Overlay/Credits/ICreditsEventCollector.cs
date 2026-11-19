using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Overlay.Credits
{
    public interface ICreditsEventCollector
    {
        Guid? CurrentStreamSessionId { get; }

        void RecordChatter(ChatMessageEventArgs chatMessage);

        Task FlushPendingChattersAsync(CancellationToken cancellationToken = default);

        Task RecordCustomEventAsync(
            string eventType,
            string? userId,
            string? username,
            string? displayName,
            decimal amount = 1,
            string? detail = null,
            string? tier = null,
            string? avatarUrl = null,
            bool isManual = false,
            bool isAnonymous = false,
            Dictionary<string, object?>? metadata = null,
            CancellationToken cancellationToken = default);

        Task<Guid> MarkManualCheckpointAsync(string? label = null, CancellationToken cancellationToken = default);

        Task<int> CleanupOldEventsAsync(int retentionDays, CancellationToken cancellationToken = default);
    }
}
