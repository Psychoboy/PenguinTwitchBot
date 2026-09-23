using Microsoft.AspNetCore.Hosting;
using NSubstitute;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Services;

public class HelpContentServiceTests
{
    private readonly IWebHostEnvironment _env;
    private readonly string _testWebRoot;

    public HelpContentServiceTests()
    {
        _env = Substitute.For<IWebHostEnvironment>();
        // Use the actual wwwroot in the project root if it exists
        var projectWwwRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PenguinTwitchBot", "wwwroot");
        if (Directory.Exists(projectWwwRoot))
        {
            _testWebRoot = Path.GetFullPath(projectWwwRoot);
        }
        else
        {
            _testWebRoot = AppContext.BaseDirectory;
        }

        _env.WebRootPath.Returns(_testWebRoot);
    }

    [Fact]
    public async Task GetTopicAsync_BotAuth_ReturnsTopicWithThreeTabs()
    {
        var service = new HelpContentService(_env);

        var topic = await service.GetTopicAsync("bot-auth");

        Assert.NotNull(topic);
        Assert.Equal("bot-auth", topic.Id);
        Assert.Equal("Twitch Authentication Guide", topic.Title);
        Assert.Equal(3, topic.Tabs.Count);
        Assert.Contains(topic.Tabs, t => t.Title == "Account Roles");
        Assert.Contains(topic.Tabs, t => t.Title == "Tokens & Refresh");
        Assert.Contains(topic.Tabs, t => t.Title == "Troubleshooting");
    }

    [Fact]
    public async Task GetTopicAsync_Integrations_ReturnsTopicWithFourTabs()
    {
        var service = new HelpContentService(_env);

        var topic = await service.GetTopicAsync("integrations");

        Assert.NotNull(topic);
        Assert.Equal("integrations", topic.Id);
        Assert.Equal("Integrations Setup Guide", topic.Title);
        Assert.Equal(4, topic.Tabs.Count);
        Assert.Contains(topic.Tabs, t => t.Title == "YouTube");
        Assert.Contains(topic.Tabs, t => t.Title == "Discord");
        Assert.Contains(topic.Tabs, t => t.Title == "Weather");
        Assert.Contains(topic.Tabs, t => t.Title == "OpenAI");
    }

    [Fact]
    public async Task GetTopicAsync_AppSettings_ReturnsTopicWithFourTabs()
    {
        var service = new HelpContentService(_env);

        var topic = await service.GetTopicAsync("app-settings");

        Assert.NotNull(topic);
        Assert.Equal("app-settings", topic.Id);
        Assert.Equal("Bot Settings Guide", topic.Title);
        Assert.Equal(4, topic.Tabs.Count);
        Assert.Contains(topic.Tabs, t => t.Title == "General & Branding");
        Assert.Contains(topic.Tabs, t => t.Title == "Data Retention");
        Assert.Contains(topic.Tabs, t => t.Title == "Feature Services");
        Assert.Contains(topic.Tabs, t => t.Title == "Scheduled Jobs & Cron");
    }

    [Fact]
    public async Task GetTopicAsync_NonExistentTopic_ReturnsNull()
    {
        var service = new HelpContentService(_env);

        var topic = await service.GetTopicAsync("invalid-non-existent-topic");

        Assert.Null(topic);
    }

    [Fact]
    public async Task GetMarkdownFileAsync_MissingFile_ReturnsGracefulFallback()
    {
        var service = new HelpContentService(_env);

        var content = await service.GetMarkdownFileAsync("non-existent-file.md");

        Assert.NotNull(content);
        Assert.Contains("being prepared", content, StringComparison.OrdinalIgnoreCase);
    }
}

