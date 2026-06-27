using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class RaffleSetWinnerCountHandlerTests
    {
        [Fact]
        public async Task ValidType_SetsWinnerCount()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleSetWinnerCountHandler(raffleService);

            var result = new RaffleOperationResult { Success = true, Status = "winner_count_updated", WinnerCount = 3 };
            raffleService.SetWinnerCountAsync("testraffle", 3).Returns(result);

            var type = new RaffleSetWinnerCountType { RaffleKey = "testraffle", WinnerCount = 3 };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("3", variables["raffle_winner_count"]);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleSetWinnerCountHandler(raffleService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task NotRunning_ReturnsNotRunningResult()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleSetWinnerCountHandler(raffleService);

            var result = RaffleOperationResult.NotRunning("missing");
            raffleService.SetWinnerCountAsync("missing", 1).Returns(result);

            var type = new RaffleSetWinnerCountType { RaffleKey = "missing", WinnerCount = 1 };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("not_running", variables["raffle_status"]);
        }
    }
}
