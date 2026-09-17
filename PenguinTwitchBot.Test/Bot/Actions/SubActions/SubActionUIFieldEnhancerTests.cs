using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Database.Bot.Models.Obs;
using PenguinTwitchBot.Database.Bot.Models.Timers;
using System.Linq;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions;

public class SubActionUIFieldEnhancerTests
{
    [Fact]
    public void GetEnhancedFields_TimerGroupType_EnhancesWithTimerGroups()
    {
        var services = new ServiceCollection();
        var timerService = Substitute.For<IAutoTimers>();
        timerService.GetTimerGroupsAsync().Returns(new List<TimerGroup>
        {
            new TimerGroup { Id = 1, Name = "Ad Timers" },
            new TimerGroup { Id = 2, Name = "Social Timers" }
        });
        services.AddSingleton(timerService);
        var provider = services.BuildServiceProvider();

        var subAction = new TimerGroupSetEnabledStateType();
        var fields = new List<SubActionUIField>
        {
            new SubActionUIField { PropertyName = nameof(TimerGroupSetEnabledStateType.TimerGroupId) }
        };

        var result = SubActionUIFieldEnhancer.GetEnhancedFields(subAction, provider);

        var timerGroupField = result.First(f => f.PropertyName == nameof(TimerGroupSetEnabledStateType.TimerGroupId));
        Assert.NotNull(timerGroupField);
        Assert.Equal("Timer Group", timerGroupField.Label);
        Assert.Equal(UIFieldType.Select, timerGroupField.FieldType);
        Assert.True(timerGroupField.Required);
        Assert.NotNull(timerGroupField.SelectOptions);
        Assert.Equal(2, timerGroupField.SelectOptions.Count);
    }

    [Fact]
    public void GetEnhancedFields_ObsBrowserSource_UsesOptionalObsInputListMethod()
    {
        var services = new ServiceCollection();
        var connectionManager = Substitute.For<IOBSConnectionManager>();
        var obs = Substitute.For<IOBSWebsocket>();
        var inputList = new List<InputBasicInfo>
        {
            new() { InputName = "Browser One", InputKind = "browser_source" },
            new() { InputName = "Browser Two", InputKind = "browser_source" }
        };

        obs.GetInputList(Arg.Any<string>()).Returns(inputList);

        var connectionConfig = new OBSConnection
        {
            Id = 1,
            Name = "Main",
            Url = "ws://localhost:4455",
            Password = "test",
            Enabled = true
        };
        var logger = Substitute.For<ILogger<ManagedOBSConnection>>();
        var connection = new ManagedOBSConnection(connectionConfig, obs, logger);
        typeof(ManagedOBSConnection).GetProperty("IsConnected")!.SetValue(connection, true);

        connectionManager.GetAllManagedConnections().Returns(new List<ManagedOBSConnection> { connection });
        connectionManager.GetAllConnectionsAsync().Returns(Task.FromResult(new List<OBSConnection> { connectionConfig }));
        services.AddSingleton(connectionManager);
        var provider = services.BuildServiceProvider();

        var subAction = new ObsSetBrowserSourceUrlType { OBSConnectionId = 1 };
        var fields = new List<SubActionUIField>
        {
            new() { PropertyName = nameof(ObsSetBrowserSourceUrlType.InputName) }
        };

        var result = SubActionUIFieldEnhancer.GetEnhancedFields(subAction, provider);

        var browserField = result.First(f => f.PropertyName == nameof(ObsSetBrowserSourceUrlType.InputName));
        Assert.Equal(UIFieldType.Select, browserField.FieldType);
        Assert.NotNull(browserField.SelectOptions);
        Assert.Contains(browserField.SelectOptions, option => option.Name == "Browser One");
        Assert.Contains(browserField.SelectOptions, option => option.Name == "Browser Two");
    }

    [Fact]
    public void GetEnhancedFields_NullSubAction_ReturnsEmpty()
    {
        SubActionType? subAction = null;

        var result = SubActionUIFieldEnhancer.GetEnhancedFields(subAction, null);

        Assert.Empty(result);
    }

    [Fact]
    public void GetEnhancedFields_TimerGroup_NullServiceProvider_ReturnsBaseFields()
    {
        var subAction = new TimerGroupSetEnabledStateType();

        var result = SubActionUIFieldEnhancer.GetEnhancedFields(subAction, null);

        Assert.NotEmpty(result);
    }

    [Fact]
    public void GetEnhancedFields_FishingModify_FishMode_EnhancesWithFishTypesAndSettings()
    {
        var services = new ServiceCollection();
        var fishingService = Substitute.For<PenguinTwitchBot.Bot.Commands.Fishing.IFishingService>();
        var shopService = Substitute.For<PenguinTwitchBot.Bot.Commands.Fishing.IFishingShopService>();

        fishingService.GetAllFishTypes().Returns(new List<PenguinTwitchBot.Database.Bot.Models.Fishing.FishType>
        {
            new() { Id = 1, Name = "Salmon", Rarity = PenguinTwitchBot.Database.Bot.Models.Fishing.FishRarity.Common, BaseGold = 10, Enabled = true },
            new() { Id = 2, Name = "Tuna", Rarity = PenguinTwitchBot.Database.Bot.Models.Fishing.FishRarity.Rare, BaseGold = 75, Enabled = true }
        });
        fishingService.GetSettings().Returns(new PenguinTwitchBot.Database.Bot.Models.Fishing.FishingSettings
        {
            RarityUncommonThreshold = 35,
            RarityRareThreshold = 60,
            RarityEpicThreshold = 110,
            RarityLegendaryThreshold = 201,
            RarityMythicalThreshold = 300
        });

        services.AddSingleton(fishingService);
        services.AddSingleton(shopService);
        var provider = services.BuildServiceProvider();

        var subAction = new FishingModifyType
        {
            TargetType = FishingModifyTargetType.Fish,
            TargetFish = "1"
        };

        var result = SubActionUIFieldEnhancer.GetEnhancedFields(subAction, provider);

        var targetFishField = result.First(f => f.PropertyName == nameof(FishingModifyType.TargetFish));
        Assert.NotNull(targetFishField.SelectOptions);
        Assert.Equal(2, targetFishField.SelectOptions.Count);
        Assert.Contains(targetFishField.SelectOptions, o => o.Id == 1 && o.Name.Contains("Salmon"));

        var rarityField = result.First(f => f.PropertyName == nameof(FishingModifyType.RarityMode));
        Assert.NotNull(rarityField.HelperText);
        Assert.Contains("201g", rarityField.HelperText);
    }

    [Fact]
    public void GetEnhancedFields_FishingModify_ShopItemMode_EnhancesWithShopItems()
    {
        var services = new ServiceCollection();
        var fishingService = Substitute.For<PenguinTwitchBot.Bot.Commands.Fishing.IFishingService>();
        var shopService = Substitute.For<PenguinTwitchBot.Bot.Commands.Fishing.IFishingShopService>();

        shopService.GetAllShopItems().Returns(new List<PenguinTwitchBot.Database.Bot.Models.Fishing.FishingShopItem>
        {
            new() { Id = 10, Name = "Basic Rod", Cost = 100, Enabled = true },
            new() { Id = 20, Name = "Golden Reel", Cost = 500, Enabled = false }
        });

        services.AddSingleton(fishingService);
        services.AddSingleton(shopService);
        var provider = services.BuildServiceProvider();

        var subAction = new FishingModifyType
        {
            TargetType = FishingModifyTargetType.ShopItem,
            TargetShopItem = "10"
        };

        var result = SubActionUIFieldEnhancer.GetEnhancedFields(subAction, provider);

        var targetShopField = result.First(f => f.PropertyName == nameof(FishingModifyType.TargetShopItem));
        Assert.NotNull(targetShopField.SelectOptions);
        Assert.Equal(2, targetShopField.SelectOptions.Count);
        Assert.Contains(targetShopField.SelectOptions, o => o.Id == 10 && o.Name.Contains("Basic Rod"));
    }
}
