using MudBlazor;
using PenguinTwitchBot.Database.Bot.Models.Queues;

namespace PenguinTwitchBot.Pages.Actions;

public partial class ActionRecipeWizard
{
    private Task CreateBlockingQueueAsync() => OpenQueueDialogAsync(null);

    private Task CreateRecommendedQueueAsync() => OpenQueueDialogAsync(SelectedRecipe?.RecommendedQueueName);

    private async Task OpenQueueDialogAsync(string? suggestedName)
    {
        var parameters = new DialogParameters<QueueEditDialog>
        {
            {
                dialog => dialog.Queue,
                new QueueConfiguration
                {
                    Name = suggestedName ?? string.Empty,
                    IsBlocking = true,
                    Enabled = true,
                    MaxConcurrentActions = 1
                }
            },
            { dialog => dialog.IsNew, true }
        };
        var dialog = await DialogService.ShowAsync<QueueEditDialog>("Create Action Queue", parameters, new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        });
        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: QueueConfiguration queue }) return;

        queue.Name = queue.Name.Trim();
        if (_queues.Any(existing => existing.Name.Equals(queue.Name, StringComparison.OrdinalIgnoreCase)))
        {
            _error = $"Queue '{queue.Name}' already exists. Select it from the queue list.";
            return;
        }

        _error = string.Empty;
        _pendingQueue = queue;
        _selectedQueueName = queue.Name;
    }
}