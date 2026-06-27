using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class RaffleSetTotalAwardHandlerTests
    {
        [Fact]
        public async Task ValidType_SetsTotalAward()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleSetTotalAwardHandler(raffleService);

            var result = new RaffleOperationResult { Success = true, Status = "total_award_updated", TotalAward = 1000 };
            raffleService.SetTotalAwardAsync("testraffle", 500).Returns(result);

            var type = new RaffleSetTotalAwardType { RaffleKey = "testraffle", TotalAward = "500" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("1000", variables["raffle_total_award"]);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleSetTotalAwardHandler(raffleService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task InvalidTotalAward_ThrowsException()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleSetTotalAwardHandler(raffleService);

            var type = new RaffleSetTotalAwardType { RaffleKey = "testraffle", TotalAward = "abc" };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }

        [Fact]
        public async Task NegativeTotalAward_ThrowsException()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleSetTotalAwardHandler(raffleService);

            var type = new RaffleSetTotalAwardType { RaffleKey = "testraffle", TotalAward = "-1" };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
