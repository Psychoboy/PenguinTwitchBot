using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class RaffleEndHandlerTests
    {
        [Fact]
        public async Task ValidType_EndsRaffle()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleEndHandler(raffleService);

            var result = new RaffleOperationResult { Success = true, Status = "ended", EntryCount = 5 };
            raffleService.EndAsync("testraffle").Returns(result);

            var type = new RaffleEndType { RaffleKey = "testraffle" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("true", variables["raffle_success"]);
            Assert.Equal("ended", variables["raffle_status"]);
            Assert.Equal("5", variables["raffle_entry_count"]);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleEndHandler(raffleService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task NotRunning_ReturnsNotFoundResult()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleEndHandler(raffleService);

            var result = RaffleOperationResult.NotRunning("testraffle");
            raffleService.EndAsync("testraffle").Returns(result);

            var type = new RaffleEndType { RaffleKey = "testraffle" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("false", variables["raffle_success"]);
            Assert.Equal("not_running", variables["raffle_status"]);
        }
    }
}
