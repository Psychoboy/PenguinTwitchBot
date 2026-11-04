using NSubstitute;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Test.Bot.Commands.TicketGames;

public class RaffleRuntimeServiceTests
{
    [Fact]
    public async Task StartAsync_AppliesExplicitPointTypeToPointGame()
    {
        var pointsSystem = Substitute.For<IPointsSystem>();
        var pointTypeId = Guid.CreateVersion7();
        pointsSystem.RegisterDefaultPointForGame("my-raffle").Returns(Task.CompletedTask);
        pointsSystem.SetPointTypeForGame("my-raffle", pointTypeId).Returns(Task.CompletedTask);
        pointsSystem.GetPointTypeForGame("my-raffle").Returns(new PointType
        {
            Id = pointTypeId,
            Name = "Tickets"
        });
        var service = new RaffleRuntimeService(pointsSystem);

        var result = await service.StartAsync(new RaffleStartRequest
        {
            RaffleKey = "my-raffle",
            PointGameName = "my-raffle",
            PointTypeId = pointTypeId
        });

        Assert.True(result.Success);
        Assert.Equal("Tickets", result.PointTypeName);
        await pointsSystem.Received(1).SetPointTypeForGame("my-raffle", pointTypeId);
    }
}