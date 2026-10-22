using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Pages.Games;
using System.Text.Json;

namespace PenguinTwitchBot.Test.Pages;

public class StarshipEventEditingTests
{
    [Fact]
    public void SharedEventDocument_PreservesExportShapeAndRejectsUnsupportedVersions()
    {
        var definition = StarshipEventCatalog.CreateDefaults()[0] with
        {
            OpenSound = new() { Override = true, Enabled = true, AudioFile = "/media/signal_custom.wav", Volume = 28 }
        };
        var json = StarshipEventDocument.Write(definition);
        Assert.Equal(json, StarshipEventEditing.Export(definition));
        var restored = StarshipEventDocument.Read(json);
        Assert.Equal(definition.EventType, restored.EventType);
        Assert.Equal(definition.Choices.Count, restored.Choices.Count);
        Assert.True(restored.OpenSound.Override);
        Assert.True(restored.OpenSound.Enabled);
        Assert.Equal("/media/signal_custom.wav", restored.OpenSound.AudioFile);
        Assert.Equal(28, restored.OpenSound.Volume);
        Assert.Throws<InvalidDataException>(() => StarshipEventDocument.Read("{\"schemaVersion\":99,\"event\":" +
            System.Text.Json.JsonSerializer.Serialize(definition, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }) + "}"));
    }

    [Fact]
    public void LegacyEventDocument_WithoutOpenSoundInheritsBuiltinCue()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(StarshipEventDocument.Write(StarshipEventCatalog.CreateDefaults()[0]))!;
        json["event"]!.AsObject().Remove("openSound");
        var restored = StarshipEventDocument.Read(json.ToJsonString());
        var effective = StarshipEventCatalog.GetEffectiveOpenSound(restored, restored.EventType);

        Assert.Equal("/media/starship_signal_detected.wav", effective.AudioFile);
        Assert.True(effective.Enabled);
    }

    [Theory]
    [InlineData("courier-relay.json")]
    [InlineData("survey-cache.json")]
    [InlineData("debris-crossing.json")]
    public async Task BundledStarterEvent_ImportsValidatesAndRendersWithoutChangingSettings(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PenguinTwitchBot", "wwwroot", "help", "starship", "examples", fileName));
        var json = await File.ReadAllTextAsync(path);
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var draft = StarshipEventEditing.Import(json, settings);
        StarshipEventEditing.ValidateDraft(settings, draft, null);

        Assert.Equal(5, settings.EventCatalog.Count);
        Assert.NotEmpty(draft.Steps);
        Assert.Contains(draft.Steps, step => step.StepId == draft.StartStepId);
        Assert.Contains(draft.Steps, step => step.Type == StarshipEventStepType.End);
        Assert.All(draft.Steps.Where(step => step.Type == StarshipEventStepType.Vote), step =>
        {
            Assert.InRange(step.Choices.Count, 2, 6);
            Assert.Contains(step.Choices, choice => choice.ChoiceId == step.DefaultChoiceId);
            Assert.All(step.Choices, choice =>
            {
                Assert.False(choice.IsRisky);
                Assert.Contains(draft.Steps, target => target.StepId == choice.NextStepId);
            });
        });
        var expeditionEvent = new StarshipExpeditionEventSnapshot
        {
            EventType = draft.EventType, Subject = StarshipExpeditionStory.GenerateSubject(12345, 2, draft),
            Sector = "Ternion Belt", LootByTier = draft.LootByTier
        };
        var templates = draft.Steps.SelectMany(step => step.Choices.Select(choice => choice.Story)
            .Append(step.Prompt).Append(step.Narrative ?? string.Empty))
            .Concat([draft.Description, draft.SuccessStory, draft.FailureStory]);
        foreach (var template in templates)
        {
            var rendered = StarshipExpeditionStory.Render(template, expeditionEvent, 12345, ["Viewer"], "Rare");
            Assert.DoesNotContain("{", rendered);
            Assert.DoesNotContain("}", rendered);
        }
        var reimported = StarshipEventEditing.Import(StarshipEventEditing.Export(draft), settings);
        StarshipEventEditing.ValidateDraft(settings, reimported, null);
    }

    [Fact]
    public void Duplicate_IsIndependentAndPreservesInheritedRewardBinding()
    {
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var original = settings.EventCatalog.Single(definition => definition.EventType == "SalvageSite");
        var copy = StarshipEventEditing.Duplicate(original, settings);
        StarshipEventEditing.ValidateDraft(settings, copy, null);
        copy.Steps[0].Choices.Clear();
        Assert.Equal(4, original.Steps[0].Choices.Count);
        Assert.Equal("SalvageSite", copy.RewardProfileId);
        Assert.Equal(original.ShipFailureEffect, copy.ShipFailureEffect);
        Assert.DoesNotContain(settings.EventCatalog, definition => definition.EventType == copy.EventType);
    }

    [Fact]
    public void ExportImport_RoundTripsIntoUniqueCustomDraftWithoutChangingCatalog()
    {
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var original = settings.EventCatalog[0];
        var json = StarshipEventEditing.Export(original);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(2, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.True(document.RootElement.GetProperty("event").GetProperty("steps").GetArrayLength() > 0);
        var first = StarshipEventEditing.Import(json, settings);
        settings.EventCatalog.Add(first);
        var second = StarshipEventEditing.Import(json, settings);
        Assert.NotEqual(first.EventType, second.EventType);
        Assert.Equal(original.Description, first.Description);
        Assert.Equal(original.Steps.Count, first.Steps.Count);
        Assert.Equal(6, settings.EventCatalog.Count);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":99,\"event\":null}")]
    [InlineData("{\"schemaVersion\":1,\"event\":{\"script\":\"evil()\"}}")]
    [InlineData("{\"schemaVersion\":1,\"event\":{\"eventType\":\"bad\",\"title\":\"Bad\",\"description\":\"D\",\"choices\":[null],\"defaultChoiceId\":\"x\"}}")]
    public void Import_RejectsMalformedOrUnsupportedDocuments(string json)
    {
        Assert.Throws<ArgumentException>(() => StarshipEventEditing.Import(json, StarshipExpeditionSettings.NormalizeAndValidate(new())));
    }

    [Fact]
    public void CustomSystemEventImport_IsValidForConfiguredSector()
    {
        var settings = new StarshipExpeditionSettings
        {
            CustomSectors = ["Stanton", "Nyx", "Pyro", "Castra", "Terra"]
        };
        StarshipExpeditionSettings.NormalizeAndValidate(settings);
        var source = StarshipEventCatalog.CreateDefaults()[0] with { AllowedSectors = ["Nyx"] };

        var draft = StarshipEventEditing.Import(StarshipEventEditing.Export(source), settings);
        StarshipEventEditing.ValidateDraft(settings, draft, null);

        Assert.Single(draft.AllowedSectors);
        Assert.Contains(draft.AllowedSectors[0], settings.CustomSectors, StringComparer.Ordinal);
        Assert.InRange(draft.Steps.Count, 1, StarshipEventGraphValidator.MaximumTotalSteps);
        Assert.Contains(draft.Steps, step => step.Type == StarshipEventStepType.End);
    }

    [Fact]
    public void ValidateDraft_RejectsStructuralChangesAndLeavesSettingsUntouched()
    {
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var original = settings.EventCatalog.Single(definition => definition.EventType == "RouteChoice");
        Assert.Throws<ArgumentException>(() => StarshipEventEditing.ValidateDraft(settings,
            original with { DefaultChoiceId = "detour" }, original.EventType));
        Assert.Equal("direct", original.DefaultChoiceId);
    }

    [Fact]
    public void Import_MissingProfileRemainsAnUnappliableDraftUntilRemapped()
    {
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var exported = settings.EventCatalog[0] with { RewardProfileId = "another-bot-profile" };
        var draft = StarshipEventEditing.Import(StarshipEventEditing.Export(exported), settings);
        Assert.Equal("another-bot-profile", draft.RewardProfileId);
        Assert.Throws<ArgumentException>(() => StarshipEventEditing.ValidateDraft(settings, draft, null));
        StarshipEventEditing.ValidateDraft(settings, draft with { RewardProfileId = "SignalDetected" }, null);
        Assert.Equal(4, settings.RewardProfiles.Count);
    }

    [Fact]
    public void Import_RejectsOversizedAndUnsafeContent()
    {
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        Assert.Throws<ArgumentException>(() => StarshipEventEditing.Import(new string('x', StarshipEventEditing.MaximumImportBytes + 1), settings));
        var unsafeEvent = settings.EventCatalog[0] with { Description = "!ban viewer" };
        Assert.Throws<ArgumentException>(() => StarshipEventEditing.Import(StarshipEventEditing.Export(unsafeEvent), settings));
    }
}