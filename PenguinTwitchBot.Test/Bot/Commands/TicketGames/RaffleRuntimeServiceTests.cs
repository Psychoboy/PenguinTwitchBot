using NSubstitute;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Test.Bot.Commands.TicketGames;

public class RaffleRuntimeServiceTests
{
    [Fact]
    public async Task StartAsync_UsesExplicitPointTypeForRaffleWithoutChangingGameMapping()
    {
        var pointsSystem = Substitute.For<IPointsSystem>();
        var pointTypeId = Guid.CreateVersion7();
        pointsSystem.GetPointTypeById(pointTypeId).Returns(new PointType
        {
            Id = pointTypeId,
            Name = "Tickets"
        });
        var service = new RaffleRuntimeService(pointsSystem);

        var result = await service.StartAsync(new RaffleStartRequest
        {
            RaffleKey = "my-raffle",
            PointGameName = "my-raffle",
            PointTypeId = pointTypeId,
            WinnerCount = 1,
            TotalAward = 10
        });

        Assert.True(result.Success);
        Assert.Equal("Tickets", result.PointTypeName);
        var duplicate = await service.StartAsync(new RaffleStartRequest
        {
            RaffleKey = "my-raffle",
            PointGameName = "my-raffle",
            PointTypeId = Guid.CreateVersion7()
        });
        Assert.Equal("already_running", duplicate.Status);
        await pointsSystem.DidNotReceive().SetPointTypeForGame(Arg.Any<string>(), Arg.Any<Guid>());
        await pointsSystem.DidNotReceive().RegisterDefaultPointForGame(Arg.Any<string>());
        await pointsSystem.Received(1).GetPointTypeById(pointTypeId);

        Assert.Equal("entered", (await service.EnterAsync("my-raffle", "viewer")).Status);
        var ended = await service.EndAsync("my-raffle");
        Assert.Equal("ended", ended.Status);
        await pointsSystem.Received(1).AddPointsByUsername("viewer", pointTypeId, 10);
    }

    [Fact]
    public async Task StartAsync_UsesExistingGameMappingWhenNoOverrideIsSelected()
    {
        var pointsSystem = Substitute.For<IPointsSystem>();
        var mappedPointTypeId = Guid.CreateVersion7();
        pointsSystem.RegisterDefaultPointForGame("legacy-raffle").Returns(Task.CompletedTask);
        pointsSystem.GetPointTypeForGame("legacy-raffle").Returns(new PointType
        {
            Id = mappedPointTypeId,
            Name = "Default Tickets"
        });
        var service = new RaffleRuntimeService(pointsSystem);

        var result = await service.StartAsync(new RaffleStartRequest
        {
            RaffleKey = "legacy-raffle",
            PointGameName = "legacy-raffle"
        });

        Assert.True(result.Success);
        Assert.Equal("Default Tickets", result.PointTypeName);
        await pointsSystem.Received(1).RegisterDefaultPointForGame("legacy-raffle");
        await pointsSystem.Received(1).GetPointTypeForGame("legacy-raffle");
        await pointsSystem.DidNotReceive().SetPointTypeForGame(Arg.Any<string>(), Arg.Any<Guid>());
    }
}