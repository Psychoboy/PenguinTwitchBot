using System.Collections.Concurrent;
using MudBlazor;

namespace PenguinTwitchBot.Services;

public record HelpTabItem(string Title, string Icon, string Markdown);

public record HelpTopic(string Id, string Title, string Icon, List<HelpTabItem> Tabs, string? Markdown = null);

public interface IHelpContentService
{
    Task<HelpTopic?> GetTopicAsync(string topicId);
    Task<string> GetMarkdownFileAsync(string relativePath);
}

public class HelpContentService(IWebHostEnvironment environment) : IHelpContentService
{
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, (string Title, string Icon, (string TabTitle, string TabIcon, string File)[] Tabs)> _topicDefinitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bot-auth"] = (
            "Bot Authentication Guide",
            Icons.Material.Filled.Security,
            [
                ("Account Roles", Icons.Material.Filled.People, "bot-auth/roles.md"),
                ("Tokens & Refresh", Icons.Material.Filled.Autorenew, "bot-auth/tokens.md"),
                ("Troubleshooting", Icons.Material.Filled.Build, "bot-auth/troubleshooting.md")
            ]
        ),
        ["integrations"] = (
            "Integrations Guide",
            Icons.Material.Filled.Extension,
            [
                ("YouTube", Icons.Material.Filled.OndemandVideo, "integrations/youtube.md"),
                ("Discord", Icons.Material.Filled.Forum, "integrations/discord.md"),
                ("Weather", Icons.Material.Filled.Cloud, "integrations/weather.md"),
                ("OpenAI", Icons.Material.Filled.Psychology, "integrations/openai.md")
            ]
        ),
        ["app-settings"] = (
            "Bot Settings Guide",
            Icons.Material.Filled.SettingsSuggest,
            [
                ("General & Branding", Icons.Material.Filled.Palette, "app-settings/general.md"),
                ("Data Retention", Icons.Material.Filled.Storage, "app-settings/retention.md"),
                ("Feature Services", Icons.Material.Filled.MiscellaneousServices, "app-settings/features.md"),
                ("Scheduled Jobs & Cron", Icons.Material.Filled.Schedule, "app-settings/cron.md")
            ]
        ),
        ["raid-history"] = (
            "Raid History Guide",
            Icons.Material.Filled.History,
            [
                ("Overview", Icons.Material.Filled.History, "raid-history/overview.md"),
                ("Filters & Cleanup", Icons.Material.Filled.FilterAlt, "raid-history/filters-cleanup.md"),
                ("Triggers & Rewards", Icons.Material.Filled.Bolt, "raid-history/triggers-rewards.md")
            ]
        ),
        ["music-player"] = (
            "Music Player Guide",
            Icons.Material.Filled.MusicNote,
            [
                ("Playback & Queue", Icons.Material.Filled.PlayCircle, "music/player.md"),
                ("Playlists & Pooling", Icons.Material.Filled.QueueMusic, "music/multi-playlist.md"),
                ("Sub-Actions & Controls", Icons.Material.Filled.Bolt, "music/subactions.md")
            ]
        ),
        ["playlists"] = (
            "Playlists Guide",
            Icons.Material.Filled.LibraryMusic,
            [
                ("Playlist Management", Icons.Material.Filled.LibraryMusic, "music/playlists.md"),
                ("YouTube Import", Icons.Custom.Brands.YouTube, "music/import.md"),
                ("Multi-Playlist Selection", Icons.Material.Filled.Checklist, "music/multi-playlist.md")
            ]
        ),
        ["banned-songs"] = (
            "Banned Songs Guide",
            Icons.Material.Filled.Block,
            [
                ("Banned Songs", Icons.Material.Filled.Block, "music/banned-songs.md"),
                ("Triggers & Automation", Icons.Material.Filled.Bolt, "music/banned-triggers.md")
            ]
        ),
        ["song-cooldowns"] = (
            "Song Cooldowns Guide",
            Icons.Material.Filled.Timer,
            [
                ("Cooldown Rules", Icons.Material.Filled.Timer, "music/cooldowns.md"),
                ("Responses & Automation", Icons.Material.Filled.Chat, "music/cooldown-responses.md")
            ]
        ),
        ["overlay-editor"] = (
            "Overlay Editor Guide",
            Icons.Material.Filled.Dashboard,
            [
                ("Canvas & Layouts", Icons.Material.Filled.Dashboard, "overlay/editor.md"),
                ("Widget Catalog", Icons.Material.Filled.Widgets, "overlay/widgets.md"),
                ("OBS & Sub-Actions", Icons.Material.Filled.SettingsInputComponent, "overlay/obs-integration.md")
            ]
        ),
        ["stream-timer"] = (
            "Stream Timer Guide",
            Icons.Material.Filled.Timer,
            [
                ("Timer Controls", Icons.Material.Filled.Timer, "stream-timer/overview.md"),
                ("Sub-Actions & Automation", Icons.Material.Filled.Bolt, "stream-timer/subactions.md")
            ]
        ),
        ["themes"] = (
            "Themes Guide",
            Icons.Material.Filled.Palette,
            [
                ("Palettes & Modes", Icons.Material.Filled.ColorLens, "themes/palettes.md"),
                ("Defaults & Presets", Icons.Material.Filled.DashboardCustomize, "themes/presets.md")
            ]
        ),
        ["websocket-settings"] = (
            "Websocket Queues Guide",
            Icons.Material.Filled.Cable,
            [
                ("Queue Capacities", Icons.Material.Filled.Layers, "websockets/queues.md"),
                ("Connections & Topics", Icons.Material.Filled.Sensors, "websockets/connections.md"),
                ("Troubleshooting", Icons.Material.Filled.Build, "websockets/troubleshooting.md")
            ]
        ),
        ["backups"] = (
            "Backups Guide",
            Icons.Material.Filled.Backup,
            [
                ("Backup & Restore", Icons.Material.Filled.Restore, "backups/overview.md"),
                ("Retention Policies", Icons.Material.Filled.Schedule, "backups/retention.md")
            ]
        ),
        ["updates-logs"] = (
            "Updates & Logs Guide",
            Icons.Material.Filled.Update,
            [
                ("Application Updates", Icons.Material.Filled.SystemUpdate, "updates/updates.md"),
                ("Release Channels", Icons.Material.Filled.AltRoute, "updates/channels.md"),
                ("Live Logs & Debugging", Icons.Material.Filled.Terminal, "updates/logs.md")
            ]
        ),
        ["validation-status"] = (
            "Validation Status Guide",
            Icons.Material.Filled.BugReport,
            [
                ("Health Checks", Icons.Material.Filled.FactCheck, "validation/overview.md"),
                ("Action Integrity Rules", Icons.Material.Filled.Rule, "validation/rules.md"),
                ("Diagnostics & Repair", Icons.Material.Filled.BuildCircle, "validation/repair.md")
            ]
        ),
        ["voices"] = (
            "Voices Guide",
            Icons.Material.Filled.RecordVoiceOver,
            [
                ("TTS Engines", Icons.Material.Filled.SettingsVoice, "voices/engines.md"),
                ("User Assigned Voices", Icons.Material.Filled.Person, "voices/user-voices.md"),
                ("Commands & Sub-Actions", Icons.Material.Filled.Bolt, "voices/commands.md")
            ]
        ),
        ["obs-connections"] = (
            "OBS Connections Guide",
            Icons.Material.Filled.Videocam,
            [
                ("WebSocket Setup", Icons.Material.Filled.Cable, "obs/connections.md"),
                ("OBS Sub-Actions", Icons.Material.Filled.Bolt, "obs/subactions.md"),
                ("Troubleshooting", Icons.Material.Filled.Build, "obs/troubleshooting.md")
            ]
        ),
        ["channel-points"] = (
            "Channel Points Guide",
            Icons.Material.Filled.Stars,
            [
                ("Custom Rewards", Icons.Material.Filled.CardGiftcard, "channel-points/rewards.md"),
                ("Triggers & Automation", Icons.Material.Filled.Bolt, "channel-points/triggers-subactions.md")
            ]
        ),
        ["point-settings"] = (
            "Point Settings Guide",
            Icons.Material.Filled.MonetizationOn,
            [
                ("Point Currencies", Icons.Material.Filled.AccountBalanceWallet, "point-settings/currencies.md"),
                ("Chat Commands", Icons.Material.Filled.Chat, "point-settings/commands.md"),
                ("Game Assignment & Sub-Actions", Icons.Material.Filled.SportsEsports, "point-settings/game-assignment.md")
            ]
        ),
        ["loyalty-bonuses"] = (
            "Loyalty Bonuses Guide",
            Icons.Material.Filled.MilitaryTech,
            [
                ("Twitch Event Bonuses", Icons.Material.Filled.Celebration, "loyalty-bonuses/events.md"),
                ("Bonus Claim (!claim)", Icons.Material.Filled.ConfirmationNumber, "loyalty-bonuses/bonus-claim.md")
            ]
        ),
        ["game-settings"] = (
            "Game Settings Guide",
            Icons.Material.Filled.SportsEsports,
            [
                ("Games Overview", Icons.Material.Filled.Gamepad, "game-settings/games-overview.md"),
                ("Costs & Odds", Icons.Material.Filled.Tune, "game-settings/costs-rates.md"),
                ("Chat Templates", Icons.Material.Filled.Message, "game-settings/chat-templates.md")
            ]
        ),
        ["raid-rewards"] = (
            "Raid Rewards Guide",
            Icons.Material.Filled.PeopleAlt,
            [
                ("Outgoing Raid Campaigns", Icons.Material.Filled.Campaign, "raid-rewards/campaigns.md"),
                ("Verification & Rules", Icons.Material.Filled.Verified, "raid-rewards/verification.md")
            ]
        ),
        ["viewers"] = (
            "Viewers Guide",
            Icons.Material.Filled.People,
            [
                ("Viewer Profiles", Icons.Material.Filled.Badge, "viewers/search-profiles.md"),
                ("IP Logs & Multi-Accounts", Icons.Material.Filled.Fingerprint, "viewers/ip-tracking.md"),
                ("Moderation Actions", Icons.Material.Filled.Gavel, "viewers/moderation-actions.md")
            ]
        ),
        ["connected-viewers"] = (
            "Connected Viewers Guide",
            Icons.Material.Filled.ConnectWithoutContact,
            [
                ("Active Sessions", Icons.Material.Filled.Wifi, "connected-viewers/sessions.md"),
                ("Session Management", Icons.Material.Filled.SettingsEthernet, "connected-viewers/management.md")
            ]
        ),
        ["auto-shoutouts"] = (
            "Auto Shoutouts Guide",
            Icons.Material.Filled.Campaign,
            [
                ("Overview & Cooldowns", Icons.Material.Filled.RecordVoiceOver, "auto-shoutouts/overview.md"),
                ("Clips & AI Shoutouts", Icons.Material.Filled.SmartToy, "auto-shoutouts/clips-ai.md"),
                ("Commands & Triggers", Icons.Material.Filled.Bolt, "auto-shoutouts/commands-triggers.md")
            ]
        ),
        ["known-bots"] = (
            "Known Bots Guide",
            Icons.Material.Filled.SmartToy,
            [
                ("Overview", Icons.Material.Filled.ListAlt, "known-bots/overview.md"),
                ("Bot Exclusions", Icons.Material.Filled.RemoveCircleOutline, "known-bots/exclusions.md")
            ]
        ),
        ["blacklist"] = (
            "Blacklist Guide",
            Icons.Material.Filled.Block,
            [
                ("Word Filters & Regex", Icons.Material.Filled.Rule, "blacklist/filters.md"),
                ("Penalties & Timeouts", Icons.Material.Filled.Timer, "blacklist/actions-timeouts.md"),
                ("Interactive Tester", Icons.Material.Filled.Science, "blacklist/filter-tester.md")
            ]
        ),
        ["wheelspin"] = (
            "Wheel Spin Guide",
            Icons.Material.Filled.PieChart,
            [
                ("Prize Wheels", Icons.Material.Filled.Casino, "wheelspin/management.md"),
                ("Viewer Name Wheel", Icons.Material.Filled.PeopleAlt, "wheelspin/viewer-wheel.md"),
                ("Commands & Triggers", Icons.Material.Filled.Bolt, "wheelspin/commands-triggers.md")
            ]
        ),
        ["giveaway-settings"] = (
            "Giveaway Settings Guide",
            Icons.Material.Filled.CardGiftcard,
            [
                ("Prize & Promotion", Icons.Material.Filled.Celebration, "giveaway-settings/prize-details.md"),
                ("Entry Rules & Points", Icons.Material.Filled.ConfirmationNumber, "giveaway-settings/entry-rules.md")
            ]
        ),
        ["giveaway-draw"] = (
            "Draw Giveaway Guide",
            Icons.Material.Filled.Casino,
            [
                ("Drawing Lifecycle", Icons.Material.Filled.PlayArrow, "giveaway-draw/draw-process.md"),
                ("Winner Display & Redraws", Icons.Material.Filled.EmojiEvents, "giveaway-draw/winner-verification.md")
            ]
        ),
        ["giveaway-history"] = (
            "Giveaway History Guide",
            Icons.Material.Filled.History,
            [
                ("Past Winners Log", Icons.Material.Filled.ListAlt, "giveaway-history/winners-log.md"),
                ("Privacy & Contact Info", Icons.Material.Filled.Lock, "giveaway-history/privacy-contact.md")
            ]
        ),
        ["giveaway-exclusions"] = (
            "Giveaway Exclusions Guide",
            Icons.Material.Filled.PersonOff,
            [
                ("Disqualifications & Cooldowns", Icons.Material.Filled.Block, "giveaway-exclusions/disqualifications.md")
            ]
        ),
        ["default-commands"] = (
            "Default Commands Guide",
            Icons.Material.Filled.Terminal,
            [
                ("Commands Overview", Icons.Material.Filled.ListAlt, "default-commands/overview.md"),
                ("Permissions & Cooldowns", Icons.Material.Filled.Security, "default-commands/permissions-cooldowns.md")
            ]
        ),
        ["external-commands"] = (
            "External Commands Guide",
            Icons.Material.Filled.Language,
            [
                ("Overview", Icons.Material.Filled.Info, "external-commands/overview.md"),
                ("Configuration & Options", Icons.Material.Filled.Tune, "external-commands/configuration.md")
            ]
        ),
        ["aliases"] = (
            "Aliases Guide",
            Icons.Material.Filled.AltRoute,
            [
                ("Aliases & Shortcuts", Icons.Material.Filled.Shortcut, "aliases/command-aliases.md")
            ]
        ),
        ["action-commands"] = (
            "Action Commands Guide",
            Icons.Material.Filled.Bolt,
            [
                ("Triggering Actions", Icons.Material.Filled.PlayArrow, "action-commands/triggering-actions.md")
            ]
        ),
        ["action-keywords"] = (
            "Keywords Guide",
            Icons.Material.Filled.Key,
            [
                ("Chat Keywords", Icons.Material.Filled.ChatBubble, "action-keywords/chat-keywords.md")
            ]
        ),
        ["audio-commands"] = (
            "Audio Commands Guide",
            Icons.Material.Filled.VolumeUp,
            [
                ("Sound Effects", Icons.Material.Filled.GraphicEq, "audio-commands/sound-effects.md")
            ]
        ),
        ["command-cooldowns"] = (
            "Command Cooldowns Guide",
            Icons.Material.Filled.Timer,
            [
                ("Cooldown Tracking", Icons.Material.Filled.HourglassEmpty, "command-cooldowns/cooldown-tracking.md")
            ]
        ),
        ["manage-actions"] = (
            "Manage Actions Guide",
            Icons.Material.Filled.Bolt,
            [
                ("Overview", Icons.Material.Filled.Info, "actions/manage-actions.md"),
                ("Triggers & Sub-Actions", Icons.Material.Filled.AccountTree, "actions/triggers-subactions.md")
            ]
        ),
        ["manage-timers"] = (
            "Timers Guide",
            Icons.Material.Filled.Timer,
            [
                ("Timers", Icons.Material.Filled.Schedule, "actions/timers.md")
            ]
        ),
        ["global-variables"] = (
            "Global Variables Guide",
            Icons.Material.Filled.Storage,
            [
                ("Variables Reference", Icons.Material.Filled.Code, "actions/global-variables.md")
            ]
        ),
        ["action-queues"] = (
            "Queues Guide",
            Icons.Material.Filled.Queue,
            [
                ("Queue Management", Icons.Material.Filled.FormatListNumbered, "actions/queues.md")
            ]
        ),
        ["action-history"] = (
            "Action History Guide",
            Icons.Material.Filled.History,
            [
                ("Execution Diagnostics", Icons.Material.Filled.Troubleshoot, "actions/history.md")
            ]
        ),
        ["fishing-admin"] = (
            "Manage Fish & Shop Guide",
            Icons.Material.Filled.Phishing,
            [
                ("Fish & Shop Management", Icons.Material.Filled.Store, "fishing/management.md"),
                ("Sub-Actions & Setup", Icons.Material.Filled.AccountTree, "fishing/subactions-setup.md"),
                ("Tournaments & Simulator", Icons.Material.Filled.EmojiEvents, "fishing/tournaments-simulator.md")
            ]
        ),
        ["image-processing"] = (
            "Manage Images Guide",
            Icons.Material.Filled.Image,
            [
                ("Image Processing", Icons.Material.Filled.Collections, "fishing/image-processing.md")
            ]
        ),
        ["fishing-balance"] = (
            "Balance Analysis Guide",
            Icons.Material.Filled.Analytics,
            [
                ("Balance & Economy", Icons.Material.Filled.QueryStats, "fishing/balance-analysis.md")
            ]
        )
    };

    public async Task<HelpTopic?> GetTopicAsync(string topicId)
    {
        if (!_topicDefinitions.TryGetValue(topicId, out var def))
        {
            return null;
        }

        var tabs = new List<HelpTabItem>();
        foreach (var (tabTitle, tabIcon, file) in def.Tabs)
        {
            var content = await GetMarkdownFileAsync(file);
            tabs.Add(new HelpTabItem(tabTitle, tabIcon, content));
        }

        return new HelpTopic(topicId, def.Title, def.Icon, tabs);
    }

    public async Task<string> GetMarkdownFileAsync(string relativePath)
    {
        var cleanPath = relativePath.TrimStart('/', '\\').Replace('\\', '/');
        if (_cache.TryGetValue(cleanPath, out var cached))
        {
            return cached;
        }

        try
        {
            var fullPath = Path.Combine(environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"), "help", cleanPath);
            if (File.Exists(fullPath))
            {
                var text = await File.ReadAllTextAsync(fullPath);
                _cache[cleanPath] = text;
                return text;
            }
        }
        catch
        {
            // Fallback gracefully on read errors
        }

        return $"*Documentation file `{cleanPath}` is being prepared.*";
    }
}

