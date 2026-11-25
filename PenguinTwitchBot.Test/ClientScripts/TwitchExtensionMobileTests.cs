using System.IO;
using System.Linq;
using Jint;
using Xunit;

namespace PenguinTwitchBot.Test.ClientScripts;

public class TwitchExtensionMobileTests
{
    private static string FindExtensionDir()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PenguinTwitchBot.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "PenguinTwitchBot", "wwwroot", "extension");
    }

    [Fact]
    public void MobileHtml_ExistsAndHasFullFeatureParityWithPanelHtml()
    {
        var extDir = FindExtensionDir();
        var mobilePath = Path.Combine(extDir, "mobile.html");
        var panelPath = Path.Combine(extDir, "panel.html");

        Assert.True(File.Exists(mobilePath), "mobile.html must exist in wwwroot/extension/");
        var mobileHtml = File.ReadAllText(mobilePath);
        var panelHtml = File.ReadAllText(panelPath);

        // Root mobile view & viewport safe-area config
        Assert.Contains("class=\"mobile-view\"", mobileHtml);
        Assert.Contains("viewport-fit=cover", mobileHtml);
        Assert.Contains("https://extension-files.twitch.tv/helper/v1/twitch-ext.min.js", mobileHtml);
        Assert.Contains("./css/extension.css", mobileHtml);
        Assert.Contains("./js/extension-api.js", mobileHtml);
        Assert.Contains("./js/panel.js", mobileHtml);

        // Full tab parity with panel.html
        string[] requiredElementIds =
        [
            "id=\"main-nav-tabs\"",
            "id=\"panel-giveaway\"",
            "id=\"panel-fishing\"",
            "id=\"panel-leaderboards\"",
            "id=\"panel-starship\"",
            "id=\"panel-commands\"",
            "id=\"giveaway-content\"",
            "id=\"fishing-sub-content\"",
            "id=\"leaderboard-content\"",
            "id=\"starship-content\"",
            "id=\"commands-list-content\""
        ];

        foreach (var idAttr in requiredElementIds)
        {
            Assert.Contains(idAttr, panelHtml);
            Assert.Contains(idAttr, mobileHtml);
        }
    }

    [Fact]
    public void ExtensionAssets_TotalSizeIsUnderTwitchMobileOneMegabyteLimit()
    {
        var extDir = FindExtensionDir();
        var totalBytes = Directory
            .EnumerateFiles(extDir, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);

        const long twitchMobileLimitBytes = 1024 * 1024; // 1 MB
        Assert.True(
            totalBytes < twitchMobileLimitBytes,
            $"Extension asset bundle ({totalBytes} bytes) must remain under Twitch's 1 MB mobile limit.");
    }

    [Fact]
    public void ExtensionCss_DefinesMobileResponsiveTouchAndLandscapeRules()
    {
        var extDir = FindExtensionDir();
        var css = File.ReadAllText(Path.Combine(extDir, "css", "extension.css"));

        Assert.Contains("#starship-content", css);
        Assert.Contains("-webkit-overflow-scrolling: touch", css);
        Assert.Contains("html.mobile-view", css);
        Assert.Contains("max-width: none", css);
        Assert.Contains("min-height: 44px", css);
        Assert.Contains("font-size: 16px", css);
        Assert.Contains("@media (orientation: landscape)", css);
    }

    [Theory]
    [InlineData("?anchor=panel&platform=mobile&mode=viewer", false, "mobile")]
    [InlineData("?anchor=panel&platform=web&mode=viewer", false, "web")]
    [InlineData("", true, "mobile")]
    [InlineData("", false, "web")]
    public void ExtensionApi_GetPlatform_DetectsMobileAndWebCorrectly(
        string searchQuery,
        bool hasMobileViewClass,
        string expectedPlatform)
    {
        var extDir = FindExtensionDir();
        var apiJs = File.ReadAllText(Path.Combine(extDir, "js", "extension-api.js"));

        var engine = new Engine();
        engine.Execute(@"
            var window = {
                location: { search: " + System.Text.Json.JsonSerializer.Serialize(searchQuery) + @" },
                twitch: {
                    ext: {
                        onAuthorized: function() {},
                        onContext: function() {},
                        configuration: { onChanged: function() {} }
                    }
                }
            };
            var document = {
                documentElement: {
                    classList: {
                        contains: function(cls) { return cls === 'mobile-view' && " + (hasMobileViewClass ? "true" : "false") + @"; }
                    }
                }
            };
            var URLSearchParams = function(qs) {
                var map = {};
                (qs || '').replace(/^\?/, '').split('&').forEach(function(pair) {
                    if (!pair) return;
                    var parts = pair.split('=');
                    map[decodeURIComponent(parts[0])] = decodeURIComponent(parts[1] || '');
                });
                this.get = function(k) { return Object.prototype.hasOwnProperty.call(map, k) ? map[k] : null; };
            };
            var URL = function(u) {
                var m = /^([a-z]+:)\/\//i.exec(u);
                if (!m) throw new Error('Invalid URL');
                this.protocol = m[1].toLowerCase();
            };
            var self = window;
        ");
        engine.Execute(apiJs);

        var platform = engine.Evaluate("window.TwitchExtApi.getPlatform()").AsString();
        Assert.Equal(expectedPlatform, platform);
        Assert.Equal("function", engine.Evaluate("typeof window.TwitchExtApi.getBaseUrl").AsString());
    }
}
