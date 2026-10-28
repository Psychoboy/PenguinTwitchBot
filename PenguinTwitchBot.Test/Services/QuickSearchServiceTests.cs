using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Timers;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Services;

public class QuickSearchServiceTests
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureRuntimeCoordinator _featureCoordinator;
    private readonly QuickSearchService _service;

    public QuickSearchServiceTests()
    {
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scope = Substitute.For<IServiceScope>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _featureCoordinator = Substitute.For<IFeatureRuntimeCoordinator>();

        _scopeFactory.CreateScope().Returns(_scope);
        _scope.ServiceProvider.Returns(_serviceProvider);
        _serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);

        _service = new QuickSearchService(_scopeFactory, _featureCoordinator);

        // Default empty returns for repositories
        _unitOfWork.Actions.GetAllAsync().Returns(new List<ActionType>());
        _unitOfWork.TimerGroups.GetAsync().Returns(new List<TimerGroup>());
        _unitOfWork.ActionCommands.GetAllAsync().Returns(new List<ActionCommand>());
        _unitOfWork.DefaultCommands.GetAllAsync().Returns(new List<DefaultCommand>());
        _unitOfWork.AudioCommands.GetAllAsync().Returns(new List<AudioCommand>());
        _unitOfWork.ExternalCommands.GetAllAsync().Returns(new List<ExternalCommands>());
        _unitOfWork.ActionKeywords.GetAllAsync().Returns(new List<ActionKeyword>());
        _unitOfWork.Counters.GetAllAsync().Returns(new List<Counter>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SearchAsync_EmptyOrWhitespaceQuery_ReturnsEmptyList(string? query)
    {
        var pageLinks = new List<QuickSearchResult>
        {
            new("Raid History", "/raidhistory", "icon", "Pages", QuickSearchResultType.Page, "Stream Tools")
        };

        var results = await _service.SearchAsync(query!, pageLinks);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_MatchesPageLinks()
    {
        var pageLinks = new List<QuickSearchResult>
        {
            new("Raid History", "/raidhistory", "icon", "Pages", QuickSearchResultType.Page, "Stream Tools"),
            new("Music Player", "/musicplayer", "icon", "Pages", QuickSearchResultType.Page, "Stream Tools"),
            new("Point Settings", "/settings/points", "icon", "Pages", QuickSearchResultType.Page, "Configuration")
        };

        var results = await _service.SearchAsync("raid", pageLinks);

        Assert.Single(results);
        Assert.Equal("Raid History", results[0].Title);
        Assert.Equal("/raidhistory", results[0].Href);
    }

    [Fact]
    public async Task SearchAsync_MatchesActions_DeepLinksWithActionId()
    {
        var actionId1 = Guid.CreateVersion7();
        var actionId2 = Guid.CreateVersion7();
        _unitOfWork.Actions.GetAllAsync().Returns(new List<ActionType>
        {
            new() { Id = actionId1, Name = "Raid Alert Action", Group = "Alerts", Enabled = true },
            new() { Id = actionId2, Name = "Song Request Action", Group = "Music", Enabled = false }
        });

        var results = await _service.SearchAsync("raid", []);

        var actionResult = results.FirstOrDefault(r => r.ResultType == QuickSearchResultType.Action);
        Assert.NotNull(actionResult);
        Assert.Equal("Raid Alert Action", actionResult.Title);
        Assert.Equal($"/actions/manage?actionId={actionId1}", actionResult.Href);
        Assert.Contains("Alerts", actionResult.Subtitle);
    }

    [Fact]
    public async Task SearchAsync_Timers_WhenFeatureEnabled_ReturnsTimersWithDeepLink()
    {
        var timerId = Guid.CreateVersion7();
        _featureCoordinator.IsEnabled(FeatureKeys.AutoTimer).Returns(true);
        _unitOfWork.TimerGroups.GetAsync().Returns(new List<TimerGroup>
        {
            new() { Id = timerId, Name = "Hydration Reminder", Active = true, IntervalMinimumSeconds = 600, IntervalMaximumSeconds = 600 }
        });

        var results = await _service.SearchAsync("hydration", []);

        var timerResult = results.FirstOrDefault(r => r.ResultType == QuickSearchResultType.Timer);
        Assert.NotNull(timerResult);
        Assert.Equal("Hydration Reminder", timerResult.Title);
        Assert.Equal($"/actions/timers?timerGroupId={timerId}", timerResult.Href);
    }

    [Fact]
    public async Task SearchAsync_Timers_WhenFeatureDisabled_DoesNotReturnTimers()
    {
        var timerId = Guid.CreateVersion7();
        _featureCoordinator.IsEnabled(FeatureKeys.AutoTimer).Returns(false);
        _unitOfWork.TimerGroups.GetAsync().Returns(new List<TimerGroup>
        {
            new() { Id = timerId, Name = "Hydration Reminder", Active = true }
        });

        var results = await _service.SearchAsync("hydration", []);

        Assert.DoesNotContain(results, r => r.ResultType == QuickSearchResultType.Timer);
    }

    [Fact]
    public async Task SearchAsync_Commands_MatchesWithAndWithoutExclamation()
    {
        var commandId = Guid.CreateVersion7();
        _unitOfWork.ActionCommands.GetAllAsync().Returns(new List<ActionCommand>
        {
            new() { Id = commandId, CommandName = "discord", Category = "Socials", Disabled = false }
        });

        // Search with exclamation prefix
        var resultsWithPrefix = await _service.SearchAsync("!discord", []);
        Assert.Contains(resultsWithPrefix, r => r.Title == "!discord" && r.Href == $"/actions/commands?commandId={commandId}");

        // Search without prefix
        var resultsWithoutPrefix = await _service.SearchAsync("discord", []);
        Assert.Contains(resultsWithoutPrefix, r => r.Title == "!discord" && r.Href == $"/actions/commands?commandId={commandId}");
    }

    [Fact]
    public async Task SearchAsync_DefaultCommands_DeepLinksToDefaultCommandEditor()
    {
        var commandId = Guid.CreateVersion7();
        _unitOfWork.DefaultCommands.GetAllAsync().Returns(new List<DefaultCommand>
        {
            new() { Id = commandId, CommandName = "help", CustomCommandName = "help", ModuleName = "General", Disabled = false }
        });

        var results = await _service.SearchAsync("help", []);

        var defaultCmdResult = results.FirstOrDefault(r => r.Href == $"/commands/default/{commandId}");
        Assert.NotNull(defaultCmdResult);
        Assert.Equal("!help", defaultCmdResult.Title);
        Assert.Contains("General", defaultCmdResult.Subtitle);
    }

    [Fact]
    public async Task SearchAsync_AudioCommands_DeepLinksToAudioCommands()
    {
        var commandId = Guid.CreateVersion7();
        _unitOfWork.AudioCommands.GetAllAsync().Returns(new List<AudioCommand>
        {
            new() { Id = commandId, CommandName = "airhorn", AudioFile = "airhorn.mp3", Disabled = false }
        });

        var results = await _service.SearchAsync("airhorn", []);

        var audioCmdResult = results.FirstOrDefault(r => r.Href == $"/actions/audiocommands?commandId={commandId}");
        Assert.NotNull(audioCmdResult);
        Assert.Equal("!airhorn", audioCmdResult.Title);
        Assert.Contains("airhorn.mp3", audioCmdResult.Subtitle);
    }

    [Fact]
    public async Task SearchAsync_Keywords_DeepLinksToKeywords()
    {
        var keywordId = Guid.CreateVersion7();
        _unitOfWork.ActionKeywords.GetAllAsync().Returns(new List<ActionKeyword>
        {
            new() { Id = keywordId, CommandName = "gg", Category = "Chat", Disabled = false }
        });

        var results = await _service.SearchAsync("gg", []);

        var keywordResult = results.FirstOrDefault(r => r.Href == $"/actions/keywords?keywordId={keywordId}");
        Assert.NotNull(keywordResult);
        Assert.Equal("gg", keywordResult.Title);
    }

    [Fact]
    public async Task SearchAsync_Counters_DeepLinksToCounters()
    {
        var counterId = Guid.CreateVersion7();
        _unitOfWork.Counters.GetAllAsync().Returns(new List<Counter>
        {
            new() { Id = counterId, CounterName = "death", DisplayName = "Death Counter", Amount = 14 }
        });

        var results = await _service.SearchAsync("death", []);

        var counterResult = results.FirstOrDefault(r => r.Href == $"/actions/counters?counterId={counterId}");
        Assert.NotNull(counterResult);
        Assert.Equal("Death Counter", counterResult.Title);
        Assert.Contains("14", counterResult.Subtitle);
    }
}

