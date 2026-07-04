using System.Collections.Concurrent;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class FishingTournamentSubActionHandlerTests
    {
        [Fact]
        public async Task StartHandler_StartsTournamentAndWritesVariables()
        {
            var fishingService = Substitute.For<IFishingService>();
            var handler = new FishingTournamentStartHandler(fishingService);
            var tournament = new FishingTournament
            {
                Id = 10,
                Name = "Spring Cup",
                Enabled = true,
                Status = FishingTournamentStatus.Active,
                PrimaryScoreCategory = FishingTournamentScoreCategory.Largest
            };

            fishingService.StartFishingTournament(10).Returns(tournament);

            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(new FishingTournamentStartType { TournamentId = 10 }, variables);

            await fishingService.Received(1).StartFishingTournament(10);
            Assert.Equal("true", variables["fishing_tournament_success"]);
            Assert.Equal("10", variables["fishing_tournament_id"]);
            Assert.Equal("Spring Cup", variables["fishing_tournament_name"]);
            Assert.Equal("Active", variables["fishing_tournament_status"]);
        }

        [Fact]
        public async Task EndHandler_EndsTournamentAndWritesVariables()
        {
            var fishingService = Substitute.For<IFishingService>();
            var handler = new FishingTournamentEndHandler(fishingService);
            var tournament = new FishingTournament
            {
                Id = 12,
                Name = "Summer Cup",
                Enabled = false,
                Status = FishingTournamentStatus.Completed,
                PrimaryScoreCategory = FishingTournamentScoreCategory.TotalWeight
            };

            fishingService.EndFishingTournament(12).Returns(tournament);

            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(new FishingTournamentEndType { TournamentId = 12 }, variables);

            await fishingService.Received(1).EndFishingTournament(12);
            Assert.Equal("true", variables["fishing_tournament_success"]);
            Assert.Equal("12", variables["fishing_tournament_id"]);
            Assert.Equal("Summer Cup", variables["fishing_tournament_name"]);
            Assert.Equal("Completed", variables["fishing_tournament_status"]);
        }
    }
}