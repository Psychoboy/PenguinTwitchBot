using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.DatabaseTools;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Services;

public class VersionCheckServiceTests
{
    private class MockHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }

    private static VersionCheckService CreateService(
        HttpResponseMessage response,
        bool includePreviewReleases = false,
        IUpdateChannelSettingsService? updateChannelSettings = null)
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler(response));

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("GitHubRelease").Returns(httpClient);

        var logger = Substitute.For<ILogger<VersionCheckService>>();
        var dbTools = Substitute.For<IDatabaseTools>();
        var backupTools = Substitute.For<IBackupTools>();
        var host = Substitute.For<IHost>();

        if (updateChannelSettings is null)
        {
            updateChannelSettings = Substitute.For<IUpdateChannelSettingsService>();
            updateChannelSettings.GetIncludePreviewReleasesAsync(Arg.Any<bool>()).Returns(Task.FromResult(includePreviewReleases));
        }

        return new VersionCheckService(factory, logger, dbTools, backupTools, updateChannelSettings, host);
    }

    private static VersionCheckService CreateService(
        string releasesJson,
        bool includePreviewReleases = false,
        IUpdateChannelSettingsService? updateChannelSettings = null)
    {
        return CreateService(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(releasesJson, Encoding.UTF8, "application/json")
        }, includePreviewReleases, updateChannelSettings);
    }

    [Fact]
    public async Task RefreshNowAsync_WhenPreviewsDisabled_PicksLatestStableRelease()
    {
        var json = @"[
            {
                ""tag_name"": ""v0.3.0-beta.1"",
                ""name"": ""v0.3.0 Beta 1"",
                ""draft"": false,
                ""prerelease"": true,
                ""body"": ""Beta notes"",
                ""published_at"": ""2026-09-01T00:00:00Z"",
                ""assets"": []
            },
            {
                ""tag_name"": ""v0.2.0"",
                ""name"": ""v0.2.0 Stable"",
                ""draft"": false,
                ""prerelease"": false,
                ""body"": ""Stable notes"",
                ""published_at"": ""2026-08-01T00:00:00Z"",
                ""assets"": []
            },
            {
                ""tag_name"": ""v0.1.0"",
                ""name"": ""v0.1.0 Older"",
                ""draft"": false,
                ""prerelease"": false,
                ""body"": ""Older notes"",
                ""published_at"": ""2026-07-01T00:00:00Z"",
                ""assets"": []
            }
        ]";

        var service = CreateService(json, includePreviewReleases: false);
        var refreshed = await service.RefreshNowAsync();

        Assert.True(refreshed);
        Assert.Equal("0.2.0", service.LatestVersion);
        Assert.Equal("Stable notes", service.LatestReleaseNotes);
        Assert.Equal(3, service.AvailableReleases.Count);

        var preRelease = service.AvailableReleases.First(r => r.TagName == "v0.3.0-beta.1");
        Assert.True(preRelease.IsPreRelease);

        var stableRelease = service.AvailableReleases.First(r => r.TagName == "v0.2.0");
        Assert.False(stableRelease.IsPreRelease);
    }

    [Fact]
    public async Task RefreshNowAsync_WhenPreviewsEnabled_PicksLatestReleaseIncludingPreRelease()
    {
        var json = @"[
            {
                ""tag_name"": ""v0.3.0-beta.1"",
                ""name"": ""v0.3.0 Beta 1"",
                ""draft"": false,
                ""prerelease"": true,
                ""body"": ""Beta notes"",
                ""published_at"": ""2026-09-01T00:00:00Z"",
                ""assets"": []
            },
            {
                ""tag_name"": ""v0.2.0"",
                ""name"": ""v0.2.0 Stable"",
                ""draft"": false,
                ""prerelease"": false,
                ""body"": ""Stable notes"",
                ""published_at"": ""2026-08-01T00:00:00Z"",
                ""assets"": []
            }
        ]";

        var service = CreateService(json, includePreviewReleases: true);
        var refreshed = await service.RefreshNowAsync();

        Assert.True(refreshed);
        Assert.Equal("0.3.0-beta.1", service.LatestVersion);
        Assert.Equal("Beta notes", service.LatestReleaseNotes);
    }

    [Fact]
    public async Task SetIncludePreviewReleasesAsync_PersistsSettingAndTriggersRefresh()
    {
        var json = @"[
            {
                ""tag_name"": ""v0.2.0"",
                ""draft"": false,
                ""prerelease"": false,
                ""body"": ""Notes"",
                ""assets"": []
            }
        ]";

        var currentValue = false;
        var settings = Substitute.For<IUpdateChannelSettingsService>();
        settings.GetIncludePreviewReleasesAsync(Arg.Any<bool>()).Returns(_ => Task.FromResult(currentValue));
        settings.SetIncludePreviewReleasesAsync(Arg.Any<bool>()).Returns(callInfo =>
        {
            currentValue = callInfo.Arg<bool>();
            return Task.CompletedTask;
        });

        var service = CreateService(json, updateChannelSettings: settings);
        var eventFired = false;
        service.VersionStatusChanged += () => eventFired = true;

        await service.SetIncludePreviewReleasesAsync(true);

        await settings.Received(1).SetIncludePreviewReleasesAsync(true);
        Assert.True(service.IncludePreviewReleases);
        Assert.True(eventFired);
    }

    [Fact]
    public async Task RefreshNowAsync_WhenNotFound_ClearsLatestVersionAndFiresEvent()
    {
        var service = CreateService(new HttpResponseMessage(HttpStatusCode.NotFound));
        var eventFired = false;
        service.VersionStatusChanged += () => eventFired = true;

        var refreshed = await service.RefreshNowAsync();

        Assert.True(refreshed);
        Assert.Empty(service.AvailableReleases);
        Assert.Null(service.LatestVersion);
        Assert.Null(service.LatestReleaseNotes);
        Assert.Null(service.LatestUpdateAssetName);
        Assert.True(eventFired);
    }

    [Fact]
    public async Task RefreshNowAsync_WhenEmptyReleasesPayload_ClearsLatestVersionAndFiresEvent()
    {
        var service = CreateService("[]");
        var eventFired = false;
        service.VersionStatusChanged += () => eventFired = true;

        var refreshed = await service.RefreshNowAsync();

        Assert.True(refreshed);
        Assert.Empty(service.AvailableReleases);
        Assert.Null(service.LatestVersion);
        Assert.Null(service.LatestReleaseNotes);
        Assert.Null(service.LatestUpdateAssetName);
        Assert.True(eventFired);
    }

    [Fact]
    public async Task RefreshNowAsync_WhenNoReleasesPermitted_ClearsLatestVersionAndFiresEvent()
    {
        var json = @"[
            {
                ""tag_name"": ""v0.3.0-beta.1"",
                ""name"": ""v0.3.0 Beta 1"",
                ""draft"": false,
                ""prerelease"": true,
                ""body"": ""Beta notes"",
                ""assets"": []
            }
        ]";

        var service = CreateService(json, includePreviewReleases: false);
        var eventFired = false;
        service.VersionStatusChanged += () => eventFired = true;

        var refreshed = await service.RefreshNowAsync();

        Assert.True(refreshed);
        Assert.Single(service.AvailableReleases);
        Assert.Null(service.LatestVersion);
        Assert.Null(service.LatestReleaseNotes);
        Assert.Null(service.LatestUpdateAssetName);
        Assert.True(eventFired);
    }
}
