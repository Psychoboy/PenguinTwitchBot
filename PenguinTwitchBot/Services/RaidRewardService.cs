using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Events;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Core;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Channel;
using PenguinTwitchBot.TwitchApi.EventSub.Websockets;
using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.EventSub;

namespace PenguinTwitchBot.Services
{
    public interface IRaidRewardService
    {
        /// <summary>
        /// Called when a raid is INITIATED. Posts the configurable announcement telling
        /// viewers which message to send, if enabled.
        /// </summary>
        Task AnnounceRaidInitiatedAsync(string targetDisplayName);
    }

    /// <summary>
    /// Raid Reward feature. When the broadcaster raids out, viewers who were present
    /// before the raid can post a configurable message in the raided channel's chat
    /// within a configurable window to earn points (once per raid event).
    ///
    /// Reuses the shared EventSub websocket session (Twitch caps subscriptions per
    /// session) and adds a temporary channel.chat.message subscription for the raid
    /// target, which is deleted when the window closes.
    /// </summary>
    public class RaidRewardService : IRaidRewardService, IHostedService
    {
        private sealed class RaidWindow
        {
            public required string TargetUserId { get; init; }
            public required string TargetDisplayName { get; init; }
            public required string SubscriptionId { get; set; }
            public required DateTime ExpiresAtUtc { get; init; }
            public required HashSet<string> EligibleUsernames { get; init; }
            public HashSet<string> AwardedUsernames { get; } = new(StringComparer.OrdinalIgnoreCase);
            public required RaidRewardConfig Config { get; init; }
            public bool SubscriptionFailed { get; set; }
        }

        private readonly ILogger<RaidRewardService> _logger;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly IEventSubWebsocketClient _eventSubClient;
        private readonly IViewerFeature _viewerFeature;
        private readonly IPointsSystem _pointsSystem;
        private readonly ITwitchService _twitchService;
        private readonly IRaidRewardSettingsService _settings;
        private readonly IModerationClient _moderationClient;
        private readonly IConfiguration _configuration;
        private readonly TimeProvider _timeProvider;

        private readonly object _windowLock = new();
        private RaidWindow? _activeWindow;
        private Timer? _expiryTimer;

        public RaidRewardService(
            ILogger<RaidRewardService> logger,
            IServiceBackbone serviceBackbone,
            IEventSubWebsocketClient eventSubClient,
            IViewerFeature viewerFeature,
            IPointsSystem pointsSystem,
            ITwitchService twitchService,
            IRaidRewardSettingsService settings,
            IModerationClient moderationClient,
            IConfiguration configuration,
            TimeProvider timeProvider)
        {
            _logger = logger;
            _serviceBackbone = serviceBackbone;
            _eventSubClient = eventSubClient;
            _viewerFeature = viewerFeature;
            _pointsSystem = pointsSystem;
            _twitchService = twitchService;
            _settings = settings;
            _moderationClient = moderationClient;
            _configuration = configuration;
            _timeProvider = timeProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _serviceBackbone.OutgoingRaidEvent += OnOutgoingRaid;
            _eventSubClient.ChannelChatMessage += OnChannelChatMessage;
            _logger.LogInformation("RaidRewardService started");
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _serviceBackbone.OutgoingRaidEvent -= OnOutgoingRaid;
            _eventSubClient.ChannelChatMessage -= OnChannelChatMessage;
            await CloseActiveWindowAsync();
        }

        /// <summary>
        /// Called by RaidTracker when a raid is INITIATED (before the raid actually fires).
        /// Posts the configurable announcement telling viewers which message to send.
        /// </summary>
        public async Task AnnounceRaidInitiatedAsync(string targetDisplayName)
        {
            var config = await _settings.GetConfigAsync();
            if (!config.Enabled || !config.PostAnnouncement)
                return;
            if (string.IsNullOrWhiteSpace(config.Message))
                return;

            var pointTypeName = await GetPointTypeNameAsync(config.PointTypeId);
            var message = BuildAnnouncement(config, targetDisplayName, pointTypeName);
            await _serviceBackbone.SendChatMessage(message);
        }

        internal async Task OnOutgoingRaid(object? sender, OutgoingRaidEventArgs e)
        {
            try
            {
                var config = await _settings.GetConfigAsync();
                if (!config.Enabled || config.PointTypeId <= 0 || string.IsNullOrWhiteSpace(config.Message))
                    return;

                // Snapshot eligible viewers: current chatters OR recently-active chatters.
                var eligible = _viewerFeature.GetCurrentViewers()
                    .Concat(_viewerFeature.GetActiveViewers())
                    .Select(UsernameNormalizer.Normalize)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var window = new RaidWindow
                {
                    TargetUserId = e.TargetUserId,
                    TargetDisplayName = e.TargetDisplayName,
                    SubscriptionId = string.Empty,
                    ExpiresAtUtc = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(Math.Max(1, config.TimeWindowMinutes)),
                    EligibleUsernames = eligible,
                    Config = config
                };

                lock (_windowLock)
                {
                    _activeWindow = window;
                }

                await CreateChatSubscriptionAsync(window);
                StartExpiryTimer(window);

                _logger.LogInformation("Raid reward window opened for {Target} until {Expiry} with {Count} eligible viewers",
                    e.TargetDisplayName, window.ExpiresAtUtc, eligible.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling outgoing raid for raid reward");
            }
        }

        private async Task CreateChatSubscriptionAsync(RaidWindow window)
        {
            var clientId = _configuration["twitchClientId"] ?? string.Empty;
            var token = _configuration["twitchAccessToken"] ?? string.Empty;
            var sessionId = _eventSubClient.SessionId;
            var tokenOwnerId = await _twitchService.GetBroadcasterUserId();

            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(tokenOwnerId))
            {
                window.SubscriptionFailed = true;
                _logger.LogWarning("Raid reward: cannot create chat subscription (no session id or broadcaster id).");
                return;
            }

            var result = await _moderationClient.CreateEventSubSubscriptionDetailedAsync(
                clientId,
                token,
                "channel.chat.message",
                "1",
                new Dictionary<string, string>
                {
                    { "broadcaster_user_id", window.TargetUserId },
                    { "user_id", tokenOwnerId }
                },
                EventSubTransportMethod.Websocket,
                sessionId);

            if (result.IsEnabled && !string.IsNullOrWhiteSpace(result.SubscriptionId))
            {
                window.SubscriptionId = result.SubscriptionId;
                _logger.LogInformation("Raid reward: subscribed to chat for {Target} (sub {SubId})", window.TargetDisplayName, result.SubscriptionId);
            }
            else
            {
                window.SubscriptionFailed = true;
                _logger.LogWarning("Raid reward: chat subscription rejected for {Target}: {Error}", window.TargetDisplayName, result.Error);
            }
        }

        private void StartExpiryTimer(RaidWindow window)
        {
            _expiryTimer?.Dispose();
            var delay = window.ExpiresAtUtc - _timeProvider.GetUtcNow().UtcDateTime;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            _expiryTimer = new Timer(_ => _ = CloseActiveWindowAsync(), null, delay, Timeout.InfiniteTimeSpan);
        }

        internal async Task OnChannelChatMessage(object? sender, ChannelChatMessageEventArgs e)
        {
            RaidWindow? window;
            lock (_windowLock)
            {
                window = _activeWindow;
            }
            if (window == null || window.SubscriptionFailed)
                return;

            var evt = e.Event;

            // Only count messages that actually occurred in the raided channel. Verified:
            // broadcaster_user_id is the channel we joined/raided; in a shared-chat session,
            // messages from OTHER (guest) channels report a different broadcaster_user_id,
            // while source_broadcaster_user_id stays null for direct messages. So filtering
            // on broadcaster_user_id == the raided target correctly scopes to that channel.
            if (!string.Equals(evt.BroadcasterUserId, window.TargetUserId, StringComparison.OrdinalIgnoreCase))
                return;

            var username = UsernameNormalizer.Normalize(evt.ChatterUserLogin);
            if (string.IsNullOrWhiteSpace(username) || !window.EligibleUsernames.Contains(username))
                return;

            // Match: message contains the configured phrase (case-insensitive). Subs may
            // also use the optional subscriber phrase.
            var text = evt.Message.Text ?? string.Empty;
            var isSub = await _viewerFeature.IsSubscriber(username);
            var matched = ContainsPhrase(text, window.Config.Message);
            if (!matched && isSub && !string.IsNullOrWhiteSpace(window.Config.SubscriberMessage))
                matched = ContainsPhrase(text, window.Config.SubscriberMessage);

            if (!matched)
                return;

            // Award once per raid event.
            if (!window.AwardedUsernames.Add(username))
                return;

            await AwardAsync(window, username, evt.ChatterUserId, evt.ChatterUserName);
        }

        private static bool ContainsPhrase(string text, string phrase)
            => text.Contains(phrase, StringComparison.OrdinalIgnoreCase);

        private async Task AwardAsync(RaidWindow window, string username, string chatterUserId, string chatterDisplayName)
        {
            try
            {
                var config = window.Config;
                var userId = chatterUserId;
                if (string.IsNullOrWhiteSpace(userId))
                {
                    userId = await _twitchService.GetUserId(username) ?? string.Empty;
                }
                if (string.IsNullOrWhiteSpace(userId))
                {
                    _logger.LogWarning("Raid reward: could not resolve user id for {Username}, skipping award", username);
                    return;
                }

                var newTotal = await _pointsSystem.AddPointsByUserId(userId, config.PointTypeId, config.PointsToAward);
                var pointTypeName = await GetPointTypeNameAsync(config.PointTypeId);

                // All awarded points are logged.
                _logger.LogInformation(
                    "RaidReward awarded: user={Username} ({UserId}) amount={Amount} pointType={PointType} (id {PointTypeId}) raidTarget={Target} newTotal={NewTotal}",
                    chatterDisplayName, userId, config.PointsToAward, pointTypeName, config.PointTypeId, window.TargetDisplayName, newTotal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Raid reward: error awarding points to {Username}", username);
            }
        }

        private async Task<string> GetPointTypeNameAsync(int pointTypeId)
        {
            var pt = await _pointsSystem.GetPointTypeById(pointTypeId);
            return pt?.Name ?? $"Type {pointTypeId}";
        }

        private string BuildAnnouncement(RaidRewardConfig config, string targetDisplayName, string pointTypeName)
        {
            var message = config.AnnouncementTemplate
                .Replace("{target}", targetDisplayName)
                .Replace("{message}", config.Message)
                .Replace("{submessage}", config.SubscriberMessage ?? string.Empty)
                .Replace("{minutes}", config.TimeWindowMinutes.ToString())
                .Replace("{points}", config.PointsToAward.ToString())
                .Replace("{pointtype}", pointTypeName);

            if (!string.IsNullOrWhiteSpace(config.SubscriberMessage))
                message += $" Subscribers can use \"{config.SubscriberMessage}\" instead!";

            return message;
        }

        private async Task CloseActiveWindowAsync()
        {
            RaidWindow? window;
            lock (_windowLock)
            {
                window = _activeWindow;
                _activeWindow = null;
            }
            _expiryTimer?.Dispose();
            _expiryTimer = null;

            if (window == null)
                return;

            if (!string.IsNullOrWhiteSpace(window.SubscriptionId))
            {
                try
                {
                    var clientId = _configuration["twitchClientId"] ?? string.Empty;
                    var token = _configuration["twitchAccessToken"] ?? string.Empty;
                    await _moderationClient.DeleteEventSubSubscriptionAsync(clientId, token, window.SubscriptionId);
                    _logger.LogInformation("Raid reward: deleted chat subscription {SubId} for {Target}", window.SubscriptionId, window.TargetDisplayName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Raid reward: failed to delete subscription {SubId}", window.SubscriptionId);
                }
            }

            _logger.LogInformation("Raid reward window closed for {Target}; awarded {Count} viewer(s)", window.TargetDisplayName, window.AwardedUsernames.Count);
        }
    }
}
