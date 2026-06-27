using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class RaffleEnterHandlerTests
    {
        [Fact]
        public async Task ValidType_EntersRaffle()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleEnterHandler(raffleService);

            var result = new RaffleOperationResult { Success = true, Status = "entered", Username = "testuser", Joined = true };
            raffleService.EnterAsync("testraffle", "testuser").Returns(result);

            var type = new RaffleEnterType { RaffleKey = "testraffle" };
            var variables = new ConcurrentDictionary<string, string>
            {
                ["OriginalEventArgs"] = "{\"Name\":\"testuser\"}"
            };

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("true", variables["raffle_joined"]);
            Assert.Equal("testuser", variables["raffle_username"]);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleEnterHandler(raffleService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task MissingUsername_ThrowsException()
        {
            var raffleService = Substitute.For<IRaffleRuntimeService>();
            var handler = new RaffleEnterHandler(raffleService);

            var type = new RaffleEnterType { RaffleKey = "testraffle" };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
