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
        private Timer? _preRaidReminderTimer;
        private int _preRaidReminderGeneration;

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
            CancelPreRaidReminder();
            await CloseActiveWindowAsync();
        }

        /// <summary>
        /// Called by RaidTracker when a raid is INITIATED (before the raid actually fires).
        /// Posts the configurable announcement telling viewers which message to send.
        /// </summary>
        public async Task AnnounceRaidInitiatedAsync(string targetDisplayName)
        {
            try
            {
                var config = await _settings.GetConfigAsync();
                if (!config.Enabled || !config.PostPreRaidAnnouncement)
                    return;
                if (string.IsNullOrWhiteSpace(config.Message))
                    return;

                await PostAnnouncementAsync(targetDisplayName, config, "pre-raid");
                StartPreRaidReminder(targetDisplayName);
            }
            catch (Exception ex)
            {
                // Never let announcement failures (settings, point lookup, chat dispatch)
                // propagate to RaidTracker.Raid and prevent the raid from starting.
                _logger.LogError(ex, "Raid reward: failed to post pre-raid announcement for {Target}", targetDisplayName);
            }
        }

        private void StartPreRaidReminder(string targetDisplayName)
        {
            CancelPreRaidReminder();
            var generation = Interlocked.Increment(ref _preRaidReminderGeneration);
            _preRaidReminderTimer = new Timer(_ => _ = SendPreRaidReminderAsync(targetDisplayName, generation), null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);
        }

        private void CancelPreRaidReminder()
        {
            Interlocked.Increment(ref _preRaidReminderGeneration);
            _preRaidReminderTimer?.Dispose();
            _preRaidReminderTimer = null;
        }

        private bool IsCurrentReminder(int generation)
            => generation == _preRaidReminderGeneration;

        private void CancelPreRaidReminderIfCurrent(int generation)
        {
            if (!IsCurrentReminder(generation)) return;
            var timer = _preRaidReminderTimer;
            _preRaidReminderTimer = null;
            timer?.Dispose();
        }

        internal async Task SendPreRaidReminderAsync(string targetDisplayName, int generation)
        {
            try
            {
                // Skip if the raid already fired (a window is open) or a newer raid superseded this reminder.
                lock (_windowLock)
                {
                    if (_activeWindow != null || !IsCurrentReminder(generation))
                    {
                        CancelPreRaidReminderIfCurrent(generation);
                        return;
                    }
                }

                var config = await _settings.GetConfigAsync();
                if (!IsCurrentReminder(generation)) return;

                if (!config.Enabled || !config.PostPreRaidAnnouncement || string.IsNullOrWhiteSpace(config.Message))
                {
                    CancelPreRaidReminderIfCurrent(generation);
                    return;
                }

                if (!IsCurrentReminder(generation)) return;
                await PostAnnouncementAsync(targetDisplayName, config, "pre-raid reminder");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Raid reward: failed to post pre-raid reminder for {Target}", targetDisplayName);
            }
            finally
            {
                CancelPreRaidReminderIfCurrent(generation);
            }
        }

        private async Task PostAnnouncementAsync(string targetDisplayName, RaidRewardConfig config, string kind)
        {
            var pointTypeName = await GetPointTypeNameAsync(config.PointTypeId);
            var message = BuildAnnouncement(config, targetDisplayName, pointTypeName);
            await _twitchService.Announcement(message);
            _logger.LogInformation("Raid reward: posted {Kind} announcement for {Target}", kind, targetDisplayName);
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

                // The raid fired; no need for the pre-raid reminder anymore.
                CancelPreRaidReminder();

                // Close any prior window (deleting its chat subscription) before swapping in the new one.
                await CloseActiveWindowAsync();

                lock (_windowLock)
                {
                    _activeWindow = window;
                }

                try
                {
                    await CreateChatSubscriptionAsync(window);
                }
                catch (Exception ex)
                {
                    window.SubscriptionFailed = true;
                    _logger.LogError(ex, "Raid reward: error creating chat subscription for {Target}", window.TargetDisplayName);
                }
                finally
                {
                    // Always schedule expiry for THIS window, even if subscription creation failed/threw.
                    StartExpiryTimer(window);
                }

                _logger.LogInformation("Raid reward window opened for {Target} until {Expiry} with {Count} eligible viewers",
                    e.TargetDisplayName, window.ExpiresAtUtc, eligible.Count);

                if (config.PostAnnouncement)
                {
                    try
                    {
                        await PostAnnouncementAsync(e.TargetDisplayName, config, "raid-start");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Raid reward: failed to post raid-start announcement for {Target}", e.TargetDisplayName);
                    }
                }
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
            _expiryTimer = new Timer(_ => FireExpiry(window), null, delay, Timeout.InfiniteTimeSpan);
        }

        // Timer callbacks can't be async; fire-and-forget the window close.
        private void FireExpiry(RaidWindow window) => _ = CloseWindowAsync(window);

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
            var text = evt.Message.Text ?? string.Empty;

            _logger.LogInformation("Raid reward chat received (window for {Target}): Chatter={Chatter} ({ChatterId}) BroadcasterId={BId} TargetId={TId} Text='{Text}'",
                window.TargetDisplayName, evt.ChatterUserLogin, evt.ChatterUserId, evt.BroadcasterUserId, window.TargetUserId, text);

            // Only count messages that actually occurred in the raided channel. Verified:
            // broadcaster_user_id is the channel we joined/raided; in a shared-chat session,
            // messages from OTHER (guest) channels report a different broadcaster_user_id,
            // while source_broadcaster_user_id stays null for direct messages. So filtering
            // on broadcaster_user_id == the raided target correctly scopes to that channel.
            if (!string.Equals(evt.BroadcasterUserId, window.TargetUserId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Raid reward chat ignored: BroadcasterUserId '{BId}' does not match TargetUserId '{TId}' for {Target}",
                    evt.BroadcasterUserId, window.TargetUserId, window.TargetDisplayName);
                return;
            }

            var username = UsernameNormalizer.Normalize(evt.ChatterUserLogin);

            if (string.IsNullOrWhiteSpace(username) || !window.EligibleUsernames.Contains(username))
            {
                _logger.LogInformation("Raid reward chat from {Chatter} in {Target}: '{Text}' -> SKIPPED (chatter '{ChatterNormalized}' not in pre-raid chatter list of {Count} viewers)",
                    evt.ChatterUserLogin, window.TargetDisplayName, text, username, window.EligibleUsernames.Count);
                return;
            }

            // Match: message contains the configured phrase (case-insensitive & punctuation-insensitive).
            // Subs may also use the optional subscriber phrase.
            var isSub = await _viewerFeature.IsSubscriber(username);
            var matched = ContainsPhrase(text, window.Config.Message);
            if (!matched && isSub && !string.IsNullOrWhiteSpace(window.Config.SubscriberMessage))
                matched = ContainsPhrase(text, window.Config.SubscriberMessage);

            if (!matched)
            {
                _logger.LogInformation("Raid reward chat from {Chatter} in {Target}: '{Text}' -> NO MATCH for phrase '{Message}' (subPhrase='{SubMessage}', isSub={IsSub})",
                    evt.ChatterUserLogin, window.TargetDisplayName, text, window.Config.Message, window.Config.SubscriberMessage ?? "", isSub);
                return;
            }

            // Award once per raid event. Reserve atomically for concurrency; roll back on failure.
            if (!window.AwardedUsernames.Add(username))
            {
                _logger.LogInformation("Raid reward chat from {Chatter} in {Target}: '{Text}' -> ALREADY AWARDED in this raid",
                    evt.ChatterUserLogin, window.TargetDisplayName, text);
                return;
            }

            _logger.LogInformation("Raid reward chat from {Chatter} in {Target}: '{Text}' -> MATCHED! Awarding points...",
                evt.ChatterUserLogin, window.TargetDisplayName, text);

            var awarded = await AwardAsync(window, username, evt.ChatterUserId, evt.ChatterUserName);
            if (!awarded)
                window.AwardedUsernames.Remove(username);
        }

        private static bool ContainsPhrase(string text, string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase) || string.IsNullOrWhiteSpace(text))
                return false;

            // 1. Direct case-insensitive substring match
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                return true;

            // 2. Normalized match (strip non-alphanumeric punctuation and collapse extra spaces)
            var normText = NormalizeForMatching(text);
            var normPhrase = NormalizeForMatching(phrase);

            if (string.IsNullOrWhiteSpace(normPhrase))
                return false;

            return normText.Contains(normPhrase, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeForMatching(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var sb = new System.Text.StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    sb.Append(c);
                else
                    sb.Append(' ');
            }
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim().ToLowerInvariant();
        }

        private async Task<bool> AwardAsync(RaidWindow window, string username, string chatterUserId, string chatterDisplayName)
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
                    return false;
                }

                var newTotal = await _pointsSystem.AddPointsByUserId(userId, config.PointTypeId, config.PointsToAward);
                var pointTypeName = await GetPointTypeNameAsync(config.PointTypeId);

                // All awarded points are logged.
                _logger.LogInformation(
                    "RaidReward awarded: user={Username} ({UserId}) amount={Amount} pointType={PointType} (id {PointTypeId}) raidTarget={Target} newTotal={NewTotal}",
                    chatterDisplayName, userId, config.PointsToAward, pointTypeName, config.PointTypeId, window.TargetDisplayName, newTotal);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Raid reward: error awarding points to {Username}", username);
                return false;
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

        private Task CloseActiveWindowAsync()
        {
            RaidWindow? window;
            lock (_windowLock)
            {
                window = _activeWindow;
                _activeWindow = null;
            }
            return CloseWindowAsync(window);
        }

        private async Task CloseWindowAsync(RaidWindow? window)
        {
            if (window == null)
                return;

            // If this window is the currently-active one, clear it (idempotent).
            lock (_windowLock)
            {
                if (ReferenceEquals(_activeWindow, window))
                    _activeWindow = null;
            }

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
