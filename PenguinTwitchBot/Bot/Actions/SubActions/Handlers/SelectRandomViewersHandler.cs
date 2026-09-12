using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class SelectRandomViewersHandler(
        IViewerFeature viewerFeature,
        IServiceBackbone serviceBackbone) : ISubActionHandler
    {
        private const string VariablePrefix = "selected_viewer_";

        public SubActionTypes SupportedType => SubActionTypes.SelectRandomViewers;

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not SelectRandomViewersType selectRandomViewers)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for SelectRandomViewersHandler");
            }

            var excludedViewers = VariableReplacer.ReplaceVariables(selectRandomViewers.ExcludedViewers, variables)
                .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var eligibleViewers = viewerFeature.GetCurrentViewers()
                .Where(viewer => !string.IsNullOrWhiteSpace(viewer) &&
                                 !excludedViewers.Contains(viewer) &&
                                 !serviceBackbone.IsKnownBot(viewer))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var index = eligibleViewers.Count - 1; index > 0; index--)
            {
                var swapIndex = Random.Shared.Next(index + 1);
                (eligibleViewers[index], eligibleViewers[swapIndex]) = (eligibleViewers[swapIndex], eligibleViewers[index]);
            }

            foreach (var key in variables.Keys.Where(key =>
                         key.StartsWith(VariablePrefix, StringComparison.OrdinalIgnoreCase) &&
                         int.TryParse(key[VariablePrefix.Length..], out _)))
            {
                variables.TryRemove(key, out _);
            }

            var selectedViewers = eligibleViewers.Take(selectRandomViewers.ViewerCount).ToList();
            for (var index = 0; index < selectedViewers.Count; index++)
            {
                variables[$"{VariablePrefix}{index + 1}"] = selectedViewers[index];
            }

            variables["selected_viewer_count"] = selectedViewers.Count.ToString();
            context?.LogMessage(subActionIndex, $"Selected {selectedViewers.Count} of {eligibleViewers.Count} eligible viewers");

            return Task.CompletedTask;
        }
    }
}