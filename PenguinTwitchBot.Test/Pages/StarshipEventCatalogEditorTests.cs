using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Pages.Components;
using PenguinTwitchBot.Pages.Games;
using PenguinTwitchBot.Shared;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Test.Pages;

public class StarshipEventCatalogEditorTests
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton(new MediaUploadService(Path.GetTempPath(), NullLogger<MediaUploadService>.Instance));
        context.ComponentFactories.AddStub<MediaUrlPicker>();
        context.Render<MudPopoverProvider>();
        return context;
    }

    [Fact]
    public async Task DuplicatePreviewApplyAndDelete_UseIsolatedValidatedDrafts()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        await editor.Find("button[aria-label='Duplicate SalvageSite']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.Equal(5, settings.EventCatalog.Count);
        var title = editor.FindComponents<MudTextField<string>>().Single(component => component.Instance.Label == "Title");
        await title.Find("input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "Archive Recovery" });
        Assert.Equal("Derelict Salvage", settings.EventCatalog.Single(definition => definition.EventType == "SalvageSite").Title);
        await editor.FindAll("button").Single(button => button.TextContent.Trim() == "Preview Event")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await editor.WaitForAssertionAsync(() =>
        {
            Assert.Contains("The damaged vessel", editor.Markup);
            Assert.Contains("Choices by step (default route highlighted)", editor.Markup);
            Assert.Contains("approach", editor.Markup);
            Assert.Contains("recovery", editor.Markup);
            Assert.Contains("default route", editor.Markup, StringComparison.OrdinalIgnoreCase);
        });
        await ClickAsync(editor, "Apply Event");
        await editor.WaitForAssertionAsync(() =>
        {
            var added = settings.EventCatalog.Single(definition => definition.Title == "Archive Recovery");
            Assert.Equal("SalvageSite", added.RewardProfileId);
            Assert.Equal(6, settings.EventCatalog.Count);
        });
        var added = settings.EventCatalog.Single(definition => definition.Title == "Archive Recovery");
        await editor.Find($"button[aria-label='Delete {added.EventType}']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await ClickAsync(editor, "Delete Event");
        Assert.Equal(5, settings.EventCatalog.Count);
    }

    [Fact]
    public async Task ApplyDraft_DisablesSaveAndIgnoresRepeatedApplyWhileCallbackIsActive()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCount = 0;
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters
            .Add(component => component.Settings, settings)
            .Add(component => component.EventApplied, EventCallback.Factory.Create<StarshipEventDefinition>(this, async _ =>
            {
                callbackCount++;
                callbackStarted.SetResult();
                await finishCallback.Task;
            })));
        await ClickAsync(editor, "Add Event");
        var saveButton = editor.FindAll("button").Single(button => button.TextContent.Trim() == "Save Event");

        var applyTask = saveButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await editor.WaitForAssertionAsync(() =>
            Assert.True(editor.FindAll("button").Single(button => button.TextContent.Trim() == "Save Event").HasAttribute("disabled")));

        await editor.InvokeAsync(async () =>
        {
            var applyDraft = typeof(StarshipEventCatalogEditor).GetMethod(
                "ApplyDraft", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            await (Task)applyDraft.Invoke(editor.Instance, null)!;
        });
        Assert.Equal(1, callbackCount);

        finishCallback.SetResult();
        await applyTask;
        await editor.WaitForAssertionAsync(() =>
            Assert.False(editor.FindAll("button").Single(button => button.TextContent.Trim() == "Save Event").HasAttribute("disabled")));
    }

    [Fact]
    public async Task EventList_ShowsEffectiveRewardProfileAndUnboundState()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var profile = new StarshipRewardProfile
        {
            Id = "signal-crew-rewards", Name = "Signal Crew Rewards", Tiers = [new() { Name = "Common", Weight = 1 }]
        };
        settings.RewardProfiles.Add(profile);
        settings.EventRewardProfileIds["SignalDetected"] = profile.Id;
        settings.EventCatalog.Add(StarshipEventEditing.NewEvent(settings));
        StarshipExpeditionSettings.NormalizeAndValidate(settings);

        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        var signalRow = editor.FindAll("tbody tr").Single(row => row.TextContent.Contains("SignalDetected", StringComparison.Ordinal));
        var customRow = editor.FindAll("tbody tr").Single(row => row.TextContent.Contains("New Event", StringComparison.Ordinal));

        Assert.Contains("Signal Crew Rewards", signalRow.TextContent);
        Assert.Contains("No rewards", customRow.TextContent);
    }

    [Fact]
    public async Task RouteFilters_OnlyShowSelectableVoyageRoutes()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var salvageIndex = settings.EventCatalog.FindIndex(definition => definition.EventType == "SalvageSite");
        settings.EventCatalog[salvageIndex] = settings.EventCatalog[salvageIndex] with { AllowedRoutes = ["Uncharted", "Frontier Route"] };
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        await editor.Find("button[aria-label='Edit SalvageSite']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var routeLabels = editor.FindComponents<MudCheckBox<bool>>()
            .Select(component => component.Instance.Label)
            .Where(label => label is "Start" or "Uncharted" or "Core Route" or "Frontier Route")
            .ToArray();

        Assert.Equal(4, routeLabels.Length);
        Assert.Contains("Start", routeLabels);
        Assert.Contains("Uncharted", routeLabels);
        Assert.Contains("Core Route", routeLabels);
        Assert.Contains("Frontier Route", routeLabels);
        Assert.Contains("Uncharted", StarshipEventCatalog.Routes);
        Assert.True(editor.FindComponents<MudCheckBox<bool>>().Single(component => component.Instance.Label == "Start").Instance.Disabled);
        Assert.True(editor.FindComponents<MudCheckBox<bool>>().Single(component => component.Instance.Label == "Uncharted").Instance.Disabled);
        var defaults = StarshipEventCatalog.CreateDefaults();
        Assert.Equal(["Uncharted", "Frontier Route"], defaults.Single(definition => definition.EventType == "SalvageSite").AllowedRoutes);
        Assert.Equal(["Uncharted", "Core Route"], defaults.Single(definition => definition.EventType == "Hazard").AllowedRoutes);

        await editor.FindComponents<MudCheckBox<bool>>().Single(component => component.Instance.Label == "Frontier Route")
            .Find("input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = false });
        await ClickAsync(editor, "Apply Event");

        await editor.WaitForAssertionAsync(() =>
            Assert.Equal(["Uncharted"], settings.EventCatalog.Single(definition => definition.EventType == "SalvageSite").AllowedRoutes));
    }

    [Fact]
    public async Task DiscardPendingDraft_ClearsDraftAndRefreshesEditor()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        await ClickAsync(editor, "Add Event");
        Assert.Contains("New Event Draft", editor.Markup);

        await editor.InvokeAsync(() => editor.Instance.DiscardPendingDraft());

        await editor.WaitForAssertionAsync(() => Assert.DoesNotContain("New Event Draft", editor.Markup));
    }

    [Fact]
    public async Task EventSoundOverride_IsExposedAndSurvivesDraftRoundTrip()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var definition = settings.EventCatalog.Single(item => item.EventType == "SalvageSite") with
        {
            OpenSound = new() { Override = true, Enabled = true, AudioFile = "/media/my_salvage_cue.wav", Volume = 35 }
        };
        settings.EventCatalog[settings.EventCatalog.FindIndex(item => item.EventType == "SalvageSite")] = definition;
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        await editor.Find("button[aria-label='Edit SalvageSite']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        var soundEditor = editor.FindComponent<DropGameSoundEditor>();

        Assert.Equal("/media/my_salvage_cue.wav", soundEditor.Instance.SoundOverride!.AudioFile);
        Assert.Equal(35, soundEditor.Instance.SoundOverride.Volume);
        Assert.True(soundEditor.Instance.IsThemeOverride);
    }

    [Fact]
    public async Task GraphStepRename_UpdatesStartAndIncomingTargets()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        await editor.Find("button[aria-label='Edit SignalDetected']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await editor.FindComponents<MudTextField<string>>().Single(component => component.Instance.Label == "Step ID")
            .Find("input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "opening" });

        await ClickAsync(editor, "Apply Event");

        await editor.WaitForAssertionAsync(() =>
        {
            var definition = settings.EventCatalog.Single(item => item.EventType == "SignalDetected");
            Assert.Equal("opening", definition.StartStepId);
            Assert.Contains(definition.Steps, step => step.StepId == "opening");
            Assert.DoesNotContain(definition.Steps.SelectMany(step => step.Choices), choice => choice.NextStepId == "approach");
        });
    }

    [Fact]
    public async Task GraphStepRename_DuplicateIdDoesNotMutateDraft()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        await editor.Find("button[aria-label='Edit SignalDetected']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        var stepId = editor.FindComponents<MudTextField<string>>().Single(component => component.Instance.Label == "Step ID");
        await stepId.Find("input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "scan-report" });

        Assert.Contains("Step ID 'scan-report' is already in use", editor.Markup);
        Assert.Equal("approach", settings.EventCatalog.Single(item => item.EventType == "SignalDetected").StartStepId);
        Assert.Contains(settings.EventCatalog.Single(item => item.EventType == "SignalDetected").Steps, step => step.StepId == "approach");
    }

    [Fact]
    public async Task DraftValidation_RejectsDuplicatesAndCapsChoicesWithoutMutatingSettings()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        await ClickAsync(editor, "Add Event");
        for (var index = 0; index < 2; index++) await ClickAsync(editor, "Add Choice");
        Assert.Equal(4, editor.FindAll("button[aria-label^='Edit choice ']").Count);
        Assert.True(editor.FindAll("button").Single(button => button.TextContent.Trim() == "Add Choice").HasAttribute("disabled"));
        await editor.FindComponents<MudTextField<string>>().Single(component => component.Instance.Label == "Event ID").Find("input")
            .ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "SignalDetected" });
        await ClickAsync(editor, "Apply Event");
        await editor.WaitForAssertionAsync(() =>
        {
            Assert.Equal(5, settings.EventCatalog.Count);
            Assert.Contains("Event IDs must be unique", editor.Markup);
        });
        await editor.FindAll("button").Single(button => button.TextContent.Trim() == "Discard Draft")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await editor.WaitForAssertionAsync(() => Assert.DoesNotContain("New Event Draft", editor.Markup));
    }

    [Fact]
    public async Task StructuralEditor_LocksMechanicsButAllowsWording()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        await editor.Find("button[aria-label='Edit RouteChoice']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.DoesNotContain(editor.FindComponents<MudNumericField<int>>(), component => component.Instance.Label == "Selection Weight");
        Assert.DoesNotContain(editor.FindComponents<MudSelect<string>>(), component => component.Instance.Label == "Default Choice");
        Assert.True(editor.FindComponents<MudTextField<string>>().Single(component => component.Instance.Label == "Choice ID").Instance.Disabled);
        Assert.DoesNotContain(editor.FindAll("button"), button => button.TextContent.Trim() == "Add Choice");
        await editor.FindComponents<MudTextField<string>>().Single(component => component.Instance.Label == "Title").Find("input")
            .ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "Choose Our Journey" });
        await ClickAsync(editor, "Apply Event");
        await editor.WaitForAssertionAsync(() =>
        {
            Assert.Equal("Choose Our Journey", settings.EventCatalog.Single(definition => definition.EventType == "RouteChoice").Title);
            Assert.Equal("core", settings.EventCatalog.Single(definition => definition.EventType == "RouteChoice").DefaultChoiceId);
        });
    }

    [Fact]
    public async Task ImportExport_OpensDraftAndDoesNotReplaceExistingEvents()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        var json = StarshipEventEditing.Export(settings.EventCatalog[0]);
        await editor.FindComponents<MudTextField<string>>().Single(component => component.Instance.Label == "Event JSON").Find("textarea")
            .ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = json });
        await ClickAsync(editor, "Import as Draft");
        Assert.Contains("New Event Draft", editor.Markup);
        Assert.Equal(5, settings.EventCatalog.Count);
        await ClickAsync(editor, "Export JSON");
        Assert.Single(context.JSInterop.Invocations["downloadFile"]);
        await ClickAsync(editor, "Apply Event");
        await editor.WaitForAssertionAsync(() =>
        {
            Assert.Equal(6, settings.EventCatalog.Count);
            Assert.Contains(settings.EventCatalog, definition => definition.EventType == "SignalDetected");
        });
    }

    [Fact]
    public async Task OwnedRewardProfile_IsAppliedAtomicallyWithEvent()
    {
        await using var context = CreateContext();
        var pointTypeId = Guid.CreateVersion7();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters
            .Add(component => component.Settings, settings)
            .Add(component => component.PointTypes, new Dictionary<Guid, string> { [pointTypeId] = "Credits" }));
        await ClickAsync(editor, "Add Event");
        await ClickAsync(editor, "Create Event Reward Profile");
        Assert.Equal(4, settings.RewardProfiles.Count);
        await ClickAsync(editor, "Apply Event");
        await editor.WaitForAssertionAsync(() =>
        {
            var added = settings.EventCatalog.Single(definition => definition.EventType == "custom-event");
            var profile = settings.RewardProfiles.Single(item => item.Id == added.RewardProfileId);
            Assert.Equal(pointTypeId, profile.Tiers[0].Rewards[0].PointTypeId);
            Assert.Equal(5, settings.RewardProfiles.Count);
        });
    }

    [Fact]
    public async Task FileImport_UsesTheSameValidatedDraftPath()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        var json = StarshipEventEditing.Export(settings.EventCatalog[0]);
        editor.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(json, "event.json"));
        await editor.WaitForAssertionAsync(() => Assert.Contains("New Event Draft", editor.Markup));
        Assert.Equal(5, settings.EventCatalog.Count);
        await ClickAsync(editor, "Apply Event");
        await editor.WaitForAssertionAsync(() => Assert.Equal(6, settings.EventCatalog.Count));
    }

    [Fact]
    public async Task Delete_RejectsRemovingLastEligibleEvent()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        settings.EventCatalog = settings.EventCatalog.Select(definition => StarshipEventCatalog.IsStructural(definition.EventType) ? definition : definition with { Enabled = false }).ToList();
        settings.EventCatalog.Add(StarshipEventEditing.NewEvent(settings));
        StarshipExpeditionSettings.NormalizeAndValidate(settings);
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        await editor.Find("button[aria-label='Delete custom-event']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await ClickAsync(editor, "Delete Event");
        Assert.Equal(6, settings.EventCatalog.Count);
        Assert.Contains("No event is eligible", editor.Markup);
    }

    [Fact]
    public async Task ClearingRequiredFields_ShowsValidationWithoutChangingSettings()
    {
        await using var context = CreateContext();
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var editor = context.Render<StarshipEventCatalogEditor>(parameters => parameters.Add(component => component.Settings, settings));
        await ClickAsync(editor, "Add Event");
        await editor.FindComponents<MudTextField<string>>().Single(component => component.Instance.Label == "Title").Find("input")
            .ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "" });
        await ClickAsync(editor, "Apply Event");
        await editor.WaitForAssertionAsync(() =>
        {
            Assert.Equal(5, settings.EventCatalog.Count);
            Assert.Contains("mud-alert", editor.Markup);
        });
    }

    private static async Task ClickAsync(IRenderedComponent<StarshipEventCatalogEditor> editor, string label) =>
        await editor.FindAll("button").Single(button => button.TextContent.Trim() == label)
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
}