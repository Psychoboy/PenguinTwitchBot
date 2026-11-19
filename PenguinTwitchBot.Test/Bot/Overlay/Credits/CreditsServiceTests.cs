using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands.Moderation;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Overlay.Credits;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Credits;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Test.Helpers;
using PenguinTwitchBot.TwitchApi.Models.Users;

namespace PenguinTwitchBot.Test.Bot.Overlay.Credits
{
    public class CreditsServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly IWebSocketMessenger _wsMessenger;
        private readonly IActionManagementService _actionMgmt;
        private readonly IAction _actionRunner;
        private readonly ICreditsEventCollector _collector;
        private readonly MemoryCache _cache;
        private readonly CreditsService _service;

        public CreditsServiceTests()
        {
            _connection = SqliteTestDatabase.CreateConnection();

            _actionMgmt = Substitute.For<IActionManagementService>();
            _actionRunner = Substitute.For<IAction>();

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection), ServiceLifetime.Scoped);
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddSingleton(_actionMgmt);
            services.AddSingleton(_actionRunner);
            _provider = services.BuildServiceProvider();

            var backbone = Substitute.For<IServiceBackbone>();
            backbone.BroadcasterName.Returns("host");
            backbone.BotName.Returns("bot");

            var knownBots = Substitute.For<IKnownBots>();
            var twitch = Substitute.For<ITwitchService>();
            twitch.GetUsersByIdsAsync(Arg.Any<IEnumerable<string>>()).Returns(new Dictionary<string, User>());
            twitch.GetUsersByLoginsAsync(Arg.Any<IEnumerable<string>>()).Returns(new Dictionary<string, User>());

            _collector = Substitute.For<ICreditsEventCollector>();
            _wsMessenger = Substitute.For<IWebSocketMessenger>();
            _cache = new MemoryCache(new MemoryCacheOptions());

            var dataBuilder = new CreditsDataBuilder(
                _provider.GetRequiredService<IServiceScopeFactory>(),
                _collector,
                new CreditsTimeWindowResolver(),
                new CreditsAvatarResolver(twitch, _cache),
                backbone,
                knownBots,
                twitch);

            _service = new CreditsService(
                _provider.GetRequiredService<IServiceScopeFactory>(),
                dataBuilder,
                _collector,
                _wsMessenger,
                Substitute.For<ILogger<CreditsService>>());
        }

        public void Dispose()
        {
            _cache.Dispose();
            _provider.Dispose();
            _connection.Close();
            _connection.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task EnsureSeededAsync_SeedsSevenThemesWithClassicCinemaDefault_AndIncludesGalacticCrawl()
        {
            await _service.EnsureSeededAsync();

            var themes = await _service.GetThemesAsync();
            Assert.Equal(7, themes.Count);

            var classic = Assert.Single(themes, t => t.BuiltInKey == CreditsBuiltInThemes.DefaultThemeKey);
            var classicCfg = CreditsDataBuilder.DeserializeThemeConfig(classic.ThemeJson);
            Assert.Equal(CreditAnimationModes.SmoothScroll, classicCfg.AnimationMode);

            var galactic = Assert.Single(themes, t => t.BuiltInKey == CreditsBuiltInThemes.GalacticCrawlKey);
            var galacticCfg = CreditsDataBuilder.DeserializeThemeConfig(galactic.ThemeJson);
            Assert.Equal(CreditAnimationModes.Crawl3D, galacticCfg.AnimationMode);
            Assert.Equal(CreditBackgroundTypes.Starfield, galacticCfg.BackgroundType);
            Assert.Equal("#FEDA4A", galacticCfg.TextColor);

            var defaultProfile = Assert.Single(await _service.GetProfilesAsync(), p => p.IsDefault);
            Assert.Equal("Default", defaultProfile.Name);
            Assert.Equal(classic.Id, defaultProfile.ThemeId);
        }

        [Fact]
        public async Task ThemeManagement_ProtectsBuiltIns_AllowsCloneResetAndJsonRoundTrip()
        {
            await _service.EnsureSeededAsync();
            var themes = await _service.GetThemesAsync();
            var classic = themes.First(t => t.BuiltInKey == CreditsBuiltInThemes.DefaultThemeKey);

            // Built-in theme cannot be deleted
            Assert.False(await _service.DeleteThemeAsync(classic.Id));

            // Edit built-in theme then reset it
            var editedCfg = CreditsDataBuilder.DeserializeThemeConfig(classic.ThemeJson);
            editedCfg.HeaderColor = "#123456";
            await _service.SaveThemeAsync(classic.Id, classic.Name, "Modified", editedCfg);
            var modified = await _service.GetThemeByIdAsync(classic.Id);
            Assert.Equal("#123456", CreditsDataBuilder.DeserializeThemeConfig(modified!.ThemeJson).HeaderColor);

            var reset = await _service.ResetBuiltInThemeAsync(classic.Id);
            Assert.NotNull(reset);
            Assert.Equal("#FFFFFF", CreditsDataBuilder.DeserializeThemeConfig(reset!.ThemeJson).HeaderColor);

            // Clone, export, delete, re-import custom theme
            var cloned = await _service.CloneThemeAsync(classic.Id, "My Custom Preset");
            Assert.False(cloned.IsBuiltIn);
            var exportedJson = await _service.ExportThemeJsonAsync(cloned.Id);
            Assert.NotNull(exportedJson);
            Assert.True(await _service.DeleteThemeAsync(cloned.Id));

            var reimported = await _service.ImportThemeAsync(exportedJson!);
            Assert.False(reimported.IsBuiltIn);
            Assert.Equal("My Custom Preset", reimported.Name);
        }

        [Fact]
        public async Task ProfileCrud_AndJsonExportImport_AndWsBroadcasts_Work()
        {
            await _service.EnsureSeededAsync();
            var defaultProfile = (await _service.GetProfilesAsync()).Single();

            var subathon = await _service.CreateProfileAsync("Subathon");
            var profileJson = await _service.ExportProfileJsonAsync(subathon.Id);
            Assert.NotNull(profileJson);

            // Delete custom works, but deleting the last remaining profile returns false
            Assert.True(await _service.DeleteProfileAsync(subathon.Id));
            Assert.False(await _service.DeleteProfileAsync(defaultProfile.Id));

            var imported = await _service.ImportProfileAsync(profileJson!);
            Assert.Equal("Subathon", imported.Name);

            await _service.PlayAsync("Default", sampleMode: CreditSampleModes.Fake);
            await _wsMessenger.Received().AddToQueue(
                WsTopics.Credits,
                Arg.Is<string>(s => s.Contains("\"type\": \"credits_play\"", StringComparison.OrdinalIgnoreCase)));

            await _service.StopAsync("Default");
            await _wsMessenger.Received().AddToQueue(
                WsTopics.Credits,
                Arg.Is<string>(s => s.Contains("\"type\": \"credits_stop\"", StringComparison.OrdinalIgnoreCase)));

            await _service.TogglePauseAsync("Default");
            await _wsMessenger.Received().AddToQueue(
                WsTopics.Credits,
                Arg.Is<string>(s => s.Contains("\"type\": \"credits_toggle_pause\"", StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public async Task TriggerCompletedAsync_DispatchesProfileAndWildcardActionsWithVariables()
        {
            var specificAction = new ActionType { Id = Guid.NewGuid(), Name = "After Default Credits" };
            var wildcardAction = new ActionType { Id = Guid.NewGuid(), Name = "After Any Credits" };

            _actionMgmt.GetActionsByTriggerTypeAndNameEnabledAsync(
                    TriggerTypes.CreditsCompleted,
                    ICreditsService.CompletedTriggerNamePrefix + "Default")
                .Returns([specificAction]);

            _actionMgmt.GetActionsByTriggerTypeAndNameEnabledAsync(
                    TriggerTypes.CreditsCompleted,
                    ICreditsService.CompletedTriggerNamePrefix + ICreditsService.AnyProfileTriggerKey)
                .Returns([wildcardAction]);

            await _service.TriggerCompletedAsync("Default", totalEntries: 27, totalSections: 4);

            await _actionRunner.Received(1).EnqueueAction(
                Arg.Is<ConcurrentDictionary<string, string>>(v =>
                    v["creditsProfile"] == "Default" &&
                    v["totalEntries"] == "27" &&
                    v["totalSections"] == "4"),
                specificAction);

            await _actionRunner.Received(1).EnqueueAction(
                Arg.Is<ConcurrentDictionary<string, string>>(v => v["creditsProfile"] == "Default"),
                wildcardAction);
        }

        [Fact]
        public async Task TriggerCompletedAsync_SanitizesLogEntry_WhenExceptionLoggedForUntrustedProfileName()
        {
            var capturingLogger = new CapturingCreditsLogger();
            var backbone = Substitute.For<IServiceBackbone>();
            backbone.BroadcasterName.Returns("host");
            backbone.BotName.Returns("bot");
            var twitch = Substitute.For<ITwitchService>();

            var dataBuilder = new CreditsDataBuilder(
                _provider.GetRequiredService<IServiceScopeFactory>(),
                _collector,
                new CreditsTimeWindowResolver(),
                new CreditsAvatarResolver(twitch, _cache),
                backbone,
                Substitute.For<IKnownBots>(),
                twitch);

            var service = new CreditsService(
                _provider.GetRequiredService<IServiceScopeFactory>(),
                dataBuilder,
                _collector,
                _wsMessenger,
                capturingLogger);

            _actionMgmt.GetActionsByTriggerTypeAndNameEnabledAsync(
                    TriggerTypes.CreditsCompleted,
                    Arg.Any<string>())
                .Returns<List<ActionType>>(_ => throw new InvalidOperationException("boom"));

            await service.TriggerCompletedAsync("Evil\r\nForgedLogLine\nNext", totalEntries: 1, totalSections: 1);

            var logged = Assert.Single(capturingLogger.Messages);
            Assert.DoesNotContain("\r", logged, StringComparison.Ordinal);
            Assert.DoesNotContain("\n", logged, StringComparison.Ordinal);
            Assert.Contains("EvilForgedLogLineNext", logged, StringComparison.Ordinal);
        }

        private sealed class CapturingCreditsLogger : ILogger<CreditsService>
        {
            public List<string> Messages { get; } = [];
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Messages.Add(formatter(state, exception));
        }
    }
}
