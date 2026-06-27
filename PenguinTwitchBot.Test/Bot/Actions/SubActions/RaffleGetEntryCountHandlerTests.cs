using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class RaffleGetEntryCountHandlerTests
    {
        [Fact]
        public async Task ValidType_GetsEntryCount()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleGetEntryCountHandler(raffleService);

            var result = new RaffleOperationResult { Success = true, Status = "entry_count", EntryCount = 42 };
            raffleService.GetEntryCountAsync("testraffle").Returns(result);

            var type = new RaffleGetEntryCountType { RaffleKey = "testraffle" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("42", variables["raffle_entry_count"]);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleGetEntryCountHandler(raffleService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task NotRunning_ReturnsNotRunningResult()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleGetEntryCountHandler(raffleService);

            var result = RaffleOperationResult.NotRunning("missing");
            raffleService.GetEntryCountAsync("missing").Returns(result);

            var type = new RaffleGetEntryCountType { RaffleKey = "missing" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("0", variables["raffle_entry_count"]);
            Assert.Equal("not_running", variables["raffle_status"]);
        }
    }
}
