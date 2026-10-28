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
            var tournamentId = Guid.CreateVersion7();
            var fishingService = Substitute.For<IFishingService>();
            var handler = new FishingTournamentStartHandler(fishingService);
            var tournament = new FishingTournament
            {
                Id = tournamentId,
                Name = "Spring Cup",
                Enabled = true,
                Status = FishingTournamentStatus.Active,
                PrimaryScoreCategory = FishingTournamentScoreCategory.Largest
            };

            fishingService.StartFishingTournament(tournamentId).Returns(tournament);

            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(new FishingTournamentStartType { TournamentId = tournamentId }, variables);

            await fishingService.Received(1).StartFishingTournament(tournamentId);
            await fishingService.DidNotReceive().CloneAndStartFishingTournament(Arg.Any<Guid>());
            Assert.Equal("true", variables["fishing_tournament_success"]);
            Assert.Equal(tournamentId.ToString(), variables["fishing_tournament_id"]);
            Assert.Equal("Spring Cup", variables["fishing_tournament_name"]);
            Assert.Equal("Active", variables["fishing_tournament_status"]);
            Assert.Equal("false", variables["fishing_tournament_started_from_template"]);
        }

        [Fact]
        public async Task StartHandler_CloneMode_ClonesAndStartsTournamentAndWritesVariables()
        {
            var templateId = Guid.CreateVersion7();
            var clonedId = Guid.CreateVersion7();
            var fishingService = Substitute.For<IFishingService>();
            var handler = new FishingTournamentStartHandler(fishingService);
            var tournament = new FishingTournament
            {
                Id = clonedId,
                Name = "Spring Cup (2026-07-05 09:00)",
                Enabled = true,
                Status = FishingTournamentStatus.Active,
                PrimaryScoreCategory = FishingTournamentScoreCategory.TotalWeight
            };

            fishingService.CloneAndStartFishingTournament(templateId).Returns(tournament);

            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(new FishingTournamentStartType { TournamentId = templateId, CloneFromTemplate = true }, variables);

            await fishingService.Received(1).CloneAndStartFishingTournament(templateId);
            await fishingService.DidNotReceive().StartFishingTournament(Arg.Any<Guid>());
            Assert.Equal("true", variables["fishing_tournament_success"]);
            Assert.Equal(clonedId.ToString(), variables["fishing_tournament_id"]);
            Assert.Equal("Spring Cup (2026-07-05 09:00)", variables["fishing_tournament_name"]);
            Assert.Equal("Active", variables["fishing_tournament_status"]);
            Assert.Equal("true", variables["fishing_tournament_started_from_template"]);
        }

        [Fact]
        public async Task EndHandler_EndsTournamentAndWritesVariables()
        {
            var tournamentId = Guid.CreateVersion7();
            var fishingService = Substitute.For<IFishingService>();
            var handler = new FishingTournamentEndHandler(fishingService);
            var tournament = new FishingTournament
            {
                Id = tournamentId,
                Name = "Summer Cup",
                Enabled = false,
                Status = FishingTournamentStatus.Completed,
                PrimaryScoreCategory = FishingTournamentScoreCategory.TotalWeight
            };

            fishingService.EndFishingTournament(tournamentId).Returns(tournament);

            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(new FishingTournamentEndType { TournamentId = tournamentId }, variables);

            await fishingService.Received(1).EndFishingTournament(tournamentId);
            Assert.Equal("true", variables["fishing_tournament_success"]);
            Assert.Equal(tournamentId.ToString(), variables["fishing_tournament_id"]);
            Assert.Equal("Summer Cup", variables["fishing_tournament_name"]);
            Assert.Equal("Completed", variables["fishing_tournament_status"]);
        }
    }
}