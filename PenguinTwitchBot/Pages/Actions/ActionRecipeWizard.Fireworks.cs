using PenguinTwitchBot.Bot.WebSocketEvents;
using PenguinTwitchBot.Database.Repository;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace PenguinTwitchBot.Pages.Actions;

public partial class ActionRecipeWizard
{
    private async Task LoadFireworksWidgetPresenceAsync()
    {
        try
        {
            await using var scope = ScopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var layouts = await unitOfWork.OverlayLayouts.GetAllWithWidgetsAsync();
            _fireworksWidgetPresent = layouts.SelectMany(layout => layout.Widgets).Any(widget =>
                widget.IsEnabled && widget.WidgetType.Equals("fireworks", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            _fireworksWidgetPresent = false;
        }
    }

    private async Task TestFireworksAsync()
    {
        if (!_fireworksWidgetPresent || _isBusy) return;
        try
        {
            var count = ParseTestValue("BurstCount", 1, 100, 1);
            var delay = ParseTestValue("DelayMilliseconds", 0, 60000, 100);
            await WebSocketMessenger.AddToQueue(new WsEvent
            {
                Event = new WsEventType { Source = "SubAction", Type = "Fireworks" },
                Data = new Dictionary<string, object>
                {
                    ["jobId"] = Guid.NewGuid().ToString("N"),
                    ["count"] = count,
                    ["delayMs"] = delay,
                    ["shell"] = GetParameter("Shell").Trim(),
                    ["size"] = GetParameter("Size").Trim()
                }
            });
            Snackbar.Add("Fireworks test show queued.", Severity.Success);
        }
        catch (Exception ex)
        {
            _error = $"Could not send the Fireworks test: {ex.Message}";
        }
    }

    private int ParseTestValue(string key, int min, int max, int fallback) =>
        int.TryParse(GetParameter(key), out var value) ? Math.Clamp(value, min, max) : fallback;
}