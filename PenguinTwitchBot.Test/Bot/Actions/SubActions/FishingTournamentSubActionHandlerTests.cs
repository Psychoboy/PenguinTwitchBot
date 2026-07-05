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
            await fishingService.DidNotReceive().CloneAndStartFishingTournament(Arg.Any<int>());
            Assert.Equal("true", variables["fishing_tournament_success"]);
            Assert.Equal("10", variables["fishing_tournament_id"]);
            Assert.Equal("Spring Cup", variables["fishing_tournament_name"]);
            Assert.Equal("Active", variables["fishing_tournament_status"]);
            Assert.Equal("false", variables["fishing_tournament_started_from_template"]);
        }

        [Fact]
        public async Task StartHandler_CloneMode_ClonesAndStartsTournamentAndWritesVariables()
        {
            var fishingService = Substitute.For<IFishingService>();
            var handler = new FishingTournamentStartHandler(fishingService);
            var tournament = new FishingTournament
            {
                Id = 22,
                Name = "Spring Cup (2026-07-05 09:00)",
                Enabled = true,
                Status = FishingTournamentStatus.Active,
                PrimaryScoreCategory = FishingTournamentScoreCategory.TotalWeight
            };

            fishingService.CloneAndStartFishingTournament(10).Returns(tournament);

            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(new FishingTournamentStartType { TournamentId = 10, CloneFromTemplate = true }, variables);

            await fishingService.Received(1).CloneAndStartFishingTournament(10);
            await fishingService.DidNotReceive().StartFishingTournament(Arg.Any<int>());
            Assert.Equal("true", variables["fishing_tournament_success"]);
            Assert.Equal("22", variables["fishing_tournament_id"]);
            Assert.Equal("Spring Cup (2026-07-05 09:00)", variables["fishing_tournament_name"]);
            Assert.Equal("Active", variables["fishing_tournament_status"]);
            Assert.Equal("true", variables["fishing_tournament_started_from_template"]);
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