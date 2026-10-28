using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Commands.Misc;

namespace PenguinTwitchBot.Pages.Components
{
    public enum SubActionReferenceKind
    {
        Action,
        TimerGroup,
        Tournament
    }

    /// <summary>Resolves the Guid references held by sub-actions (actions, timer groups, tournaments) to display names.</summary>
    public sealed class SubActionReferenceNames(IServiceProvider services)
    {
        // Migrated legacy ids overlap between tables, so each kind needs its own map.
        private readonly Dictionary<(SubActionReferenceKind Kind, Guid Id), string> _names = [];

        public async Task LoadAsync()
        {
            _names.Clear();
            await TryLoad(async () =>
                Add(SubActionReferenceKind.Action, (await services.GetRequiredService<IActionManagementService>().GetAllActionsAsync()).Select(a => (a.Id, a.Name))));
            await TryLoad(async () =>
                Add(SubActionReferenceKind.TimerGroup, (await services.GetRequiredService<IAutoTimers>().GetTimerGroupsAsync()).Select(t => (t.Id, t.Name))));
            await TryLoad(async () =>
                Add(SubActionReferenceKind.Tournament, (await services.GetRequiredService<IFishingService>().GetAllFishingTournaments()).Select(t => (t.Id, t.Name))));
        }

        public string Resolve(SubActionReferenceKind kind, Guid id) =>
            _names.TryGetValue((kind, id), out var name) ? name : id.ToString();

        private void Add(SubActionReferenceKind kind, IEnumerable<(Guid Id, string Name)> items)
        {
            foreach (var (id, name) in items)
                _names[(kind, id)] = name;
        }

        // A failed lookup must not break the editor; the description falls back to the raw id.
        private static async Task TryLoad(Func<Task> load)
        {
            try { await load(); } catch { }
        }
    }
}
