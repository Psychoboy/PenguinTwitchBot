using MudBlazor;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Pages.Components;
using PenguinTwitchBot.Services;
using PenguinTwitchBot.Shared;

namespace PenguinTwitchBot.Pages.Actions;

public partial class ActionRecipeWizard
{
    private static TriggerType CloneTrigger(TriggerType trigger) => new()
    {
        Name = trigger.Name,
        Type = trigger.Type,
        Configuration = trigger.Configuration,
        Enabled = true,
        CommandId = trigger.CommandId,
        DefaultCommandId = trigger.DefaultCommandId,
        KeywordId = trigger.KeywordId,
        TimerGroupId = trigger.TimerGroupId
    };

    private async Task RemoveAdditionalTriggerAsync(TriggerType trigger)
    {
        _additionalTriggers.Remove(trigger);
        await LoadAvailableVariablesAsync();
    }

    private async Task AddAdditionalTriggerAsync()
    {
        var parameters = new DialogParameters<AddTriggerDialog>
        {
            { dialog => dialog.DisableResourceCreation, true }
        };
        if (SelectedRecipe?.AllowedTriggerTypes is { } allowedTriggerTypes)
            parameters.Add(dialog => dialog.AllowedTriggerTypes, allowedTriggerTypes.ToList());
        if (SelectedRecipe?.AllowedTwitchEventNames is { } allowedTwitchEventNames)
            parameters.Add(dialog => dialog.AllowedTwitchEventNames, allowedTwitchEventNames.ToList());
        var dialog = await DialogService.ShowAsync<AddTriggerDialog>("Add Trigger", parameters, new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        });
        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: not null }) return;

        dynamic data = result.Data;
        var trigger = new TriggerType
        {
            Name = (string)data.TriggerName,
            Type = (TriggerTypes)data.TriggerType,
            Configuration = (string)data.Configuration,
            Enabled = true,
            CommandId = (Guid?)data.CommandId,
            DefaultCommandId = (Guid?)data.DefaultCommandId,
            KeywordId = (Guid?)data.KeywordId,
            TimerGroupId = (Guid?)data.TimerGroupId
        };
        _additionalTriggers.Add(trigger);
        await LoadAvailableVariablesAsync();
    }

    private async Task OpenSoundLibraryAsync()
    {
        var parameters = new DialogParameters<MediaBrowserDialog>
        {
            { dialog => dialog.SelectionMode, true },
            { dialog => dialog.AudioOnly, true }
        };
        var dialog = await DialogService.ShowAsync<MediaBrowserDialog>("Select Sound", parameters, new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true
        });
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: MediaFileInfo selectedFile })
        {
            SetParameter("File", selectedFile.RelativeUrl);
        }
    }

    private async Task OpenTwitchSoundLibraryAsync()
    {
        var parameters = new DialogParameters<MediaBrowserDialog>
        {
            { dialog => dialog.SelectionMode, true },
            { dialog => dialog.AudioOnly, true }
        };
        var dialog = await DialogService.ShowAsync<MediaBrowserDialog>("Select Sound", parameters, new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true
        });
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: MediaFileInfo selectedFile })
            SetParameter("SoundFile", selectedFile.RelativeUrl);
    }

    private void ClearTwitchSound() => SetParameter("SoundFile", string.Empty);

    private async Task OpenKeywordSoundLibraryAsync()
    {
        var parameters = new DialogParameters<MediaBrowserDialog>
        {
            { dialog => dialog.SelectionMode, true },
            { dialog => dialog.AudioOnly, true }
        };
        var dialog = await DialogService.ShowAsync<MediaBrowserDialog>("Select Sound", parameters, new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true
        });
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: MediaFileInfo selectedFile })
            SetParameter("SoundFile", selectedFile.RelativeUrl);
    }

    private void ClearKeywordSound() => SetParameter("SoundFile", string.Empty);
}