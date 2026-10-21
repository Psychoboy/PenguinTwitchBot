using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Pages.Games;

namespace PenguinTwitchBot.Test.Pages;

public class StarshipEventEditingTests
{
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
        Assert.InRange(draft.Choices.Count, 2, 6);
        Assert.All(draft.Choices, choice => Assert.False(choice.IsRisky));
        var defaultChoice = Assert.Single(draft.Choices, choice => choice.ChoiceId == draft.DefaultChoiceId);
        Assert.False(defaultChoice.ContinuesEvent);
        var expeditionEvent = new StarshipExpeditionEventSnapshot
        {
            EventType = draft.EventType, Subject = StarshipExpeditionStory.GenerateSubject(12345, 2, draft),
            Sector = "Ternion Belt", LootByTier = draft.LootByTier
        };
        var templates = draft.Choices.Select(choice => choice.Story).Concat([draft.Description, draft.SuccessStory, draft.FailureStory]);
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
        copy.Choices.Clear();
        Assert.Equal(4, original.Choices.Count);
        Assert.Equal("SalvageSite", copy.RewardProfileId);
        Assert.DoesNotContain(settings.EventCatalog, definition => definition.EventType == copy.EventType);
    }

    [Fact]
    public void ExportImport_RoundTripsIntoUniqueCustomDraftWithoutChangingCatalog()
    {
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var original = settings.EventCatalog[0];
        var json = StarshipEventEditing.Export(original);
        var first = StarshipEventEditing.Import(json, settings);
        settings.EventCatalog.Add(first);
        var second = StarshipEventEditing.Import(json, settings);
        Assert.NotEqual(first.EventType, second.EventType);
        Assert.Equal(original.Description, first.Description);
        Assert.Equal(original.Choices.Count, first.Choices.Count);
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