using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Fishing Tournament End",
        description: "End a fishing tournament and settle rewards",
        icon: "mdi-trophy-award",
        color: "Warning",
        tableName: "subactions_fishingtournamentend")]
    public class FishingTournamentEndType : SubActionType, ISubActionUIProvider
    {
        public FishingTournamentEndType()
        {
            SubActionTypes = SubActionTypes.FishingTournamentEnd;
        }

        public int TournamentId { get; set; }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            var tournamentOptions = GetTournamentOptions(serviceProvider);

            return [
                new()
                {
                    PropertyName = nameof(TournamentId),
                    Label = "Tournament",
                    FieldType = tournamentOptions.Count > 0 ? UIFieldType.Select : UIFieldType.Number,
                    Required = true,
                    Min = 1,
                    HelperText = "Fishing tournament to end",
                    SelectOptions = tournamentOptions.Count > 0 ? tournamentOptions : null
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
            ];
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(TournamentId), TournamentId },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(TournamentId), out var tournamentId) && int.TryParse(tournamentId?.ToString(), out var parsedTournamentId))
            {
                TournamentId = parsedTournamentId;
            }

            if (values.TryGetValue(nameof(Enabled), out var enabled))
            {
                Enabled = enabled as bool? ?? true;
            }
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(TournamentId), out var tournamentId) ||
                !int.TryParse(tournamentId?.ToString(), out var parsedTournamentId) ||
                parsedTournamentId <= 0)
            {
                return "Tournament ID is required";
            }

            return null;
        }

        private static List<SelectOption> GetTournamentOptions(IServiceProvider? serviceProvider)
        {
            if (serviceProvider == null)
            {
                return [];
            }

            try
            {
                using var scope = serviceProvider.CreateScope();
                var fishingServiceType = Type.GetType("PenguinTwitchBot.Bot.Commands.Fishing.IFishingService, PenguinTwitchBot");
                if (fishingServiceType == null)
                {
                    return [];
                }

                var fishingService = scope.ServiceProvider.GetService(fishingServiceType);
                if (fishingService == null)
                {
                    return [];
                }

                var getAllFishingTournaments = fishingServiceType.GetMethod("GetAllFishingTournaments", Type.EmptyTypes);
                if (getAllFishingTournaments == null)
                {
                    return [];
                }

                var task = getAllFishingTournaments.Invoke(fishingService, []) as Task;
                if (task == null)
                {
                    return [];
                }

                task.GetAwaiter().GetResult();
                var result = task.GetType().GetProperty("Result")?.GetValue(task) as IEnumerable<FishingTournament>;
                var tournaments = result?.ToList() ?? [];

                return tournaments
                    .OrderByDescending(tournament => tournament.StartsAtUtc)
                    .ThenBy(tournament => tournament.Name)
                    .Select(tournament => new SelectOption
                    {
                        Id = tournament.Id,
                        Name = $"{tournament.Name} ({tournament.Status})"
                    })
                    .ToList();
            }
            catch
            {
                return [];
            }
        }
    }
}