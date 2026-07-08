using PenguinTwitchBot.Database.Bot.Models.Queues;

namespace PenguinTwitchBot.Bot.Queues
{
    public static class ActionExecutionLogFilter
    {
        public static IEnumerable<ActionExecutionLog> Apply(
            IEnumerable<ActionExecutionLog> source,
            string? searchString,
            ActionExecutionState? filterState,
            int? actionId = null)
        {
            var filtered = source;

            if (actionId.HasValue)
            {
                filtered = filtered.Where(log => log.ActionId == actionId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                filtered = filtered.Where(log => log.ActionName.Contains(searchString, StringComparison.OrdinalIgnoreCase));
            }

            if (filterState.HasValue)
            {
                filtered = filtered.Where(log => log.State == filterState.Value);
            }

            return filtered.OrderByDescending(log => log.EnqueuedAt);
        }
    }
}
