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

