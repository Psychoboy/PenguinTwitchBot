using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ForEachViewerHandler(
        IActionManagementService actionService,
        IAction action,
        IViewerFeature viewerFeature,
        IServiceBackbone serviceBackbone) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ForEachViewer;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ForEachViewerType forEachViewer)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for ForEachViewerHandler");
            }

            if (!forEachViewer.ActionId.HasValue || forEachViewer.ActionId.Value == 0)
            {
                throw new SubActionHandlerException(subAction, "No action ID provided to ForEachViewerHandler");
            }

            // Validate the action exists before iterating viewers
            var actionName = (await actionService.GetActionByIdAsync(forEachViewer.ActionId.Value))?.Name
                ?? throw new SubActionHandlerException(subAction, "No action found with ID: {ActionId}", forEachViewer.ActionId);

            List<string> viewers = [];
            switch (forEachViewer.ViewerScope)
            {
                case "ActiveViewers":
                    viewers = viewerFeature.GetActiveViewers();
                    break;
                case "Subscribers":
                    var allViewers = viewerFeature.GetCurrentViewers();
                    var viewerTasks = allViewers.Select(async v => (Viewer: v, IsSub: await viewerFeature.IsSubscriber(v)));
                    var viewerResults = await Task.WhenAll(viewerTasks);
                    viewers = [.. viewerResults.Where(r => r.IsSub).Select(r => r.Viewer)];
                    break;
                default:
                    viewers = viewerFeature.GetCurrentViewers();
                    break;
            }

            context?.LogMessage(subActionIndex, $"Running action '{actionName}' for {viewers.Count} viewers (scope: {forEachViewer.ViewerScope})");

            foreach (var viewer in viewers)
            {
                if (serviceBackbone.IsKnownBot(viewer)) continue;
                if(string.IsNullOrWhiteSpace(viewer)) continue;

                // Fetch a fresh ActionType instance per viewer so each queued entry
                // gets its own independent SubActions list with no shared mutable state.
                var actionItem = await actionService.GetActionByIdAsync(forEachViewer.ActionId.Value);
                if (actionItem == null) continue;

                var viewerVars = new ConcurrentDictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase)
                {
                    ["user"] = viewer
                };

                await action.EnqueueAction(viewerVars, actionItem, context?.ActionLogId, subActionIndex);
            }
        }
    }
}
