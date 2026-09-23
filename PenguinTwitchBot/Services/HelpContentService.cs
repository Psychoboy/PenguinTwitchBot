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
            "Twitch Authentication Guide",
            Icons.Material.Filled.Security,
            [
                ("Account Roles", Icons.Material.Filled.People, "bot-auth/roles.md"),
                ("Tokens & Refresh", Icons.Material.Filled.Autorenew, "bot-auth/tokens.md"),
                ("Troubleshooting", Icons.Material.Filled.Build, "bot-auth/troubleshooting.md")
            ]
        ),
        ["integrations"] = (
            "Integrations Setup Guide",
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

