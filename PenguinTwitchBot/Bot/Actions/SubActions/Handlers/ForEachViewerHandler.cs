using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
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
            switch (forEachViewer.ViewerScope.ToLower())
            {
                case "activeviewers":
                    viewers = viewerFeature.GetActiveViewers();
                    break;
                case "subscribers":
                    var allViewers = viewerFeature.GetCurrentViewers();
                    {
                        using var gate = new SemaphoreSlim(25); // Limit concurrency to avoid overwhelming Twitch API
                        var viewerTasks = allViewers.Select(async v =>
                        {
                            await gate.WaitAsync();
                            try
                            {
                                return (Viewer: v, IsSub: await viewerFeature.IsSubscriber(v));
                            }
                            finally
                            {
                                gate.Release();
                            }
                        });
                        var viewerResults = await Task.WhenAll(viewerTasks);
                        viewers = [.. viewerResults.Where(r => r.IsSub).Select(r => r.Viewer)];
                    }
                    break;
                case "allviewers":
                    viewers = viewerFeature.GetCurrentViewers();
                    break;
                default:
                    throw new SubActionHandlerException(subAction, $"Unsupported viewer scope: {forEachViewer.ViewerScope}");
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
