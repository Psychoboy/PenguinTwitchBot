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
}
