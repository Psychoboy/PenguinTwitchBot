using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;
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
