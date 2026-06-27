using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class RaffleStartHandlerTests
    {
        [Fact]
        public async Task ValidType_StartsRaffle()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleStartHandler(raffleService);

            var result = new RaffleOperationResult { Success = true, Status = "started", EntryCount = 0 };
            raffleService.StartAsync(Arg.Is<RaffleStartRequest>(r =>
                r.RaffleKey == "test" &&
                r.RaffleName == "My Raffle" &&
                r.WinnerCount == 2 &&
                r.TotalAward == 100
            )).Returns(result);

            var type = new RaffleStartType { RaffleKey = "test", RaffleName = "My Raffle", WinnerCount = 2, TotalAward = "100" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("true", variables["raffle_success"]);
            Assert.Equal("started", variables["raffle_status"]);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleStartHandler(raffleService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task InvalidTotalAward_ThrowsException()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleStartHandler(raffleService);

            var type = new RaffleStartType { RaffleKey = "test", RaffleName = "My Raffle", WinnerCount = 1, TotalAward = "abc" };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }

        [Fact]
        public async Task NegativeTotalAward_ThrowsException()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleStartHandler(raffleService);

            var type = new RaffleStartType { RaffleKey = "test", RaffleName = "My Raffle", WinnerCount = 1, TotalAward = "-1" };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
