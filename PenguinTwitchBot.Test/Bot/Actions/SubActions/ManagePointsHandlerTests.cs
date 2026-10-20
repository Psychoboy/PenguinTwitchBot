using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Points;
using NSubstitute;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions;

public class ManagePointsHandlerTests
{
    private readonly IPointsSystem _pointsSystem = Substitute.For<IPointsSystem>();
    private readonly IViewerFeature _viewerFeature = Substitute.For<IViewerFeature>();
    private readonly ManagePointsHandler _handler;

    public ManagePointsHandlerTests()
    {
        _handler = new ManagePointsHandler(_pointsSystem, _viewerFeature);
        _pointsSystem.GetPointTypeByName("Coins").Returns(new PointType { Id = 7, Name = "Coins" });
        _viewerFeature.GetViewerByUserId("user-1").Returns(new Viewer { UserId = "user-1", Username = "viewer" });
    }

    [Fact]
    public async Task Add_ResolvesMathExpressionAndUsesTriggeringUser()
    {
        var subAction = new ManagePointsType
        {
            PointTypeName = "Coins",
            Operation = "Add",
            TargetUser = "%UserId%",
            Amount = "$math(%Score% * 2)$"
        };
        var variables = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UserId"] = "user-1",
            ["Score"] = "12"
        };

        await _handler.ExecuteAsync(subAction, variables);

        await _pointsSystem.Received(1).AddPointsByUserId("user-1", 7, 24);
    }

    [Fact]
    public async Task Remove_ReportsInsufficientBalanceWithoutChangingPoints()
    {
        var subAction = new ManagePointsType
        {
            PointTypeName = "Coins",
            Operation = "Remove",
            TargetUser = "%UserId%",
            Amount = "15"
        };
        var variables = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UserId"] = "user-1"
        };
        _pointsSystem.RemovePointsFromUserByUserId("user-1", 7, 15).Returns(false);

        await Assert.ThrowsAsync<SubActionUserFacingException>(() => _handler.ExecuteAsync(subAction, variables));
    }

    [Fact]
    public async Task Set_AllowsZeroBalance()
    {
        var subAction = new ManagePointsType
        {
            PointTypeName = "Coins",
            Operation = "Set",
            TargetUser = "%UserId%",
            Amount = "0"
        };
        var variables = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UserId"] = "user-1"
        };

        await _handler.ExecuteAsync(subAction, variables);

        await _pointsSystem.Received(1).SetPointsByUserId("user-1", 7, 0);
    }

    [Fact]
    public async Task AddActive_UsesSelectedPointTypeAndAmount()
    {
        var subAction = new ManagePointsType
        {
            PointTypeName = "Coins",
            Operation = "AddActive",
            Amount = "5"
        };

        await _handler.ExecuteAsync(subAction, new ConcurrentDictionary<string, string>());

        await _pointsSystem.Received(1).AddPointsToActiveUsers(7, 5);
    }

    [Fact]
    public async Task Add_RejectsNegativeAmount()
    {
        var subAction = new ManagePointsType
        {
            PointTypeName = "Coins",
            Operation = "Add",
            TargetUser = "%UserId%",
            Amount = "-1"
        };
        var variables = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UserId"] = "user-1"
        };

        await Assert.ThrowsAsync<SubActionUserFacingException>(() => _handler.ExecuteAsync(subAction, variables));
        await _pointsSystem.DidNotReceive().AddPointsByUserId(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<long>());
    }
}