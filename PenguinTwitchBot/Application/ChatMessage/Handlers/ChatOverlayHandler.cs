using System.Text.Json;
using System.Text.Json.Serialization;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Services.Chat;

namespace PenguinTwitchBot.Application.ChatMessage.Handlers
{
    /// <summary>
    /// Broadcasts chat messages to overlay clients via the WebSocket messenger.
    /// </summary>
    public class ChatOverlayHandler(
        IWebSocketMessenger webSocketMessenger,
        IChatColorService chatColorService) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public async Task Handle(ReceivedChatMessage request, CancellationToken cancellationToken)
        {
            var e = request.EventArgs;

            // ResolvedColor may be pre-set by TwitchWebsocketHostedService, but
            // chatColorService is the authoritative source so we call it again here
            // to handle any path that created the event without going through ProcessChatMessage.
            var color = chatColorService.GetOrAssignColor(e.UserId, e.ResolvedColor?.StartsWith('#') == true ? e.ResolvedColor : null);

            var message = new ChatOverlayMessage
            {
                Type = "chat_message",
                Id = e.MessageId,
                UserId = e.UserId,
                DisplayName = e.DisplayName,
                Color = color,
                Badges = e.Badges.Select(b => new ChatOverlayBadgeDto
                {
                    SetId = b.SetId,
                    Id = b.Id,
                }).ToList(),
                Fragments = e.Fragments.Select(f => new ChatOverlayFragmentDto
                {
                    Type = f.Type,
                    Text = f.Text,
                    EmoteId = f.EmoteId,
                    EmoteUrl = f.EmoteUrl,
                    EmoteProvider = f.EmoteProvider,
                    CheerAmount = f.CheerAmount,
                    CheerColor = f.CheerColor,
                }).ToList(),
            };

            var json = JsonSerializer.Serialize(message, JsonOptions);
            await webSocketMessenger.AddToQueue(json);
        }

        // ---------- Private DTOs ----------

        private sealed class ChatOverlayMessage
        {
            public string Type { get; set; } = "";
            public string Id { get; set; } = "";
            public string UserId { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string Color { get; set; } = "";
            public List<ChatOverlayBadgeDto> Badges { get; set; } = [];
            public List<ChatOverlayFragmentDto> Fragments { get; set; } = [];
        }

        private sealed class ChatOverlayBadgeDto
        {
            public string SetId { get; set; } = "";
            public string Id { get; set; } = "";
        }

        private sealed class ChatOverlayFragmentDto
        {
            public string Type { get; set; } = "";
            public string Text { get; set; } = "";
            public string? EmoteId { get; set; }
            public string? EmoteUrl { get; set; }
            public string? EmoteProvider { get; set; }
            public int? CheerAmount { get; set; }
            public string? CheerColor { get; set; }
        }
    }
}
