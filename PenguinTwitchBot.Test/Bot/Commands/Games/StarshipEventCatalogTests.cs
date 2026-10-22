using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Test.Bot.Commands.Games;

public class StarshipEventCatalogTests
{
    [Theory]
    [InlineData("RouteChoice", "/media/starship_route_choice.wav")]
    [InlineData("StationStop", "/media/starship_station_dock.wav")]
    [InlineData("SignalDetected", "/media/starship_signal_detected.wav")]
    [InlineData("SalvageSite", "/media/starship_salvage_found.wav")]
    [InlineData("Hazard", "/media/starship_hazard_warning.wav")]
    [InlineData("courier-relay", "/media/starship_courier_relay.wav")]
    [InlineData("survey-cache", "/media/starship_survey_cache.wav")]
    [InlineData("debris-crossing", "/media/starship_debris_crossing.wav")]
    [InlineData("custom-event", "/media/starship_custom_event.wav")]
    public void EventOpenSound_UsesGeneratedDefaultUnlessCustomOverrideOrMute(string eventType, string expectedFile)
    {
        var defaults = StarshipEventCatalog.GetEffectiveOpenSound(null, eventType);
        Assert.True(defaults.Enabled);
        Assert.Equal(expectedFile, defaults.AudioFile);

        var custom = new StarshipEventDefinition(eventType, eventType, "A test event.",
            [new() { ChoiceId = "go", Label = "Go" }, new() { ChoiceId = "pass", Label = "Pass", EndsWithoutReward = true }], "pass")
        {
            OpenSound = new() { Override = true, Enabled = true, AudioFile = "/media/my_cue.wav", Volume = 37 }
        };
        var overrideSound = StarshipEventCatalog.GetEffectiveOpenSound(custom, eventType);
        Assert.True(overrideSound.Enabled);
        Assert.Equal("/media/my_cue.wav", overrideSound.AudioFile);
        Assert.Equal(37, overrideSound.Volume);

        var muted = custom with { OpenSound = new() { Override = true, Enabled = false, AudioFile = "/media/my_cue.wav", Volume = 37 } };
        Assert.False(StarshipEventCatalog.GetEffectiveOpenSound(muted, eventType).Enabled);
    }

    [Fact]
    public void EventOpenSound_InheritsConfiguredEventTypeDefault()
    {
        var configuredDefault = new DropGameSoundConfig { Enabled = true, AudioFile = "/media/type_default.wav", Volume = 29 };

        var sound = StarshipEventCatalog.GetEffectiveOpenSound(null, "custom-event", configuredDefault);

        Assert.True(sound.Enabled);
        Assert.Equal("/media/type_default.wav", sound.AudioFile);
        Assert.Equal(29, sound.Volume);
    }

    [Fact]
    public void EventGraph_AllowsBranchesToShareStepsAndCostOverrides()
    {
        var definition = GraphEvent();

        var choices = StarshipEventGraphValidator.Validate(definition);

        Assert.Equal(3, choices.Count);
        Assert.Equal(5, definition.DefaultStepSuppliesCost);
        Assert.Equal(0, definition.Steps.Single(step => step.StepId == "shared").SuppliesCostOverride);
    }

    [Theory]
    [InlineData("missing-target")]
    [InlineData("cycle")]
    [InlineData("unreachable")]
    public void EventGraph_RejectsInvalidTargetsCyclesAndUnreachableSteps(string defect)
    {
        var definition = GraphEvent();
        if (defect == "missing-target")
        {
            definition.Steps[0].Choices[0] = new() { ChoiceId = "scan", Label = "Scan", NextStepId = "missing" };
        }
        else if (defect == "cycle")
        {
            definition.Steps.Single(step => step.StepId == "shared").NextStepId = "start";
        }
        else
        {
            definition.Steps.Add(new() { StepId = "unreachable-end", Type = StarshipEventStepType.End });
        }

        Assert.Throws<ArgumentException>(() => StarshipEventGraphValidator.Validate(definition));
    }

    private static StarshipEventDefinition GraphEvent() => new(
        "graph-event", "Graph Event", "Choose a route.", [], "")
    {
        StartStepId = "start",
        DefaultStepSuppliesCost = 5,
        Steps =
        [
            new()
            {
                StepId = "start", Type = StarshipEventStepType.Vote, DefaultChoiceId = "retreat",
                Choices =
                [
                    new() { ChoiceId = "scan", Label = "Scan", NextStepId = "shared" },
                    new() { ChoiceId = "trace", Label = "Trace", NextStepId = "shared" },
                    new() { ChoiceId = "retreat", Label = "Retreat", NextStepId = "end" }
                ]
            },
            new() { StepId = "shared", Type = StarshipEventStepType.Automatic, NextStepId = "end", SuppliesCostOverride = 0 },
            new() { StepId = "end", Type = StarshipEventStepType.End }
        ]
    };

    [Fact]
    public void GeneratedStarshipWavs_AreInMediaManagerAndHaveValidWaveHeaders()
    {
        var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PenguinTwitchBot", "wwwroot"));
        var media = new MediaUploadService(webRoot, NullLogger<MediaUploadService>.Instance);
        var files = new[]
        {
            "starship_route_choice.wav", "starship_station_dock.wav", "starship_signal_detected.wav",
            "starship_salvage_found.wav", "starship_hazard_warning.wav", "starship_courier_relay.wav",
            "starship_survey_cache.wav", "starship_debris_crossing.wav", "starship_custom_event.wav", "starship_emergency_return.wav",
            "starship_boarding_open.wav"
        };

        foreach (var file in files)
        {
            var path = Path.Combine(media.MediaDirectory, file);
            Assert.True(File.Exists(path), $"Missing generated sound {file} in the Media Manager directory.");
            Assert.True(MediaUploadService.ValidateAudioSignature(File.ReadAllBytes(path), ".wav"));
            Assert.Equal($"/media/{file}", media.ResolveSoundUrl($"/media/{file}"));
            var mediaEntry = Assert.Single(media.GetMediaFiles(), item => item.FileName == file);
            Assert.True(mediaEntry.IsAudio);
        }
    }

    [Theory]
    [InlineData("Uncharted")]
    [InlineData("Direct Route")]
    [InlineData("Survey Route")]
    public void DefaultCatalog_PreservesLegacySeededSelection(string route)
    {
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var legacy = route switch
        {
            "Direct Route" => new[] { "SignalDetected", "Hazard" },
            "Survey Route" => ["SignalDetected", "SalvageSite"],
            _ => ["SignalDetected", "SalvageSite", "Hazard"]
        };
        for (var seed = 0; seed < 100; seed++)
        {
            var snapshot = new StarshipExpeditionSnapshot
            {
                Seed = seed, CurrentRoute = route, CurrentSector = "Kepler Reach", CurrentLeg = 1
            };
            for (var sequence = 2; sequence <= 24; sequence++)
            {
                var expected = legacy[StarshipExpeditionStory.StableIndex(seed, sequence, $"event-type:{route}", legacy.Length)];
                Assert.Equal(expected, StarshipEventCatalog.Select(settings.EventCatalog, snapshot, sequence).EventType);
            }
        }
    }

    [Fact]
    public void WeightedSelection_UsesStableOrderingRoutesSectorsAndEnabledState()
    {
        var catalog = StarshipEventCatalog.CreateDefaults();
        catalog.RemoveAll(definition => !StarshipEventCatalog.IsStructural(definition.EventType));
        catalog.Add(Custom("alpha") with { Weight = 3, AllowedRoutes = ["Direct Route"], AllowedSectors = ["Kepler Reach"] });
        catalog.Add(Custom("beta") with { Weight = 1 });
        catalog.Add(Custom("disabled") with { Enabled = false, Weight = 10 });
        for (var seed = 0; seed < 100; seed++)
        {
            var snapshot = new StarshipExpeditionSnapshot { Seed = seed, CurrentLeg = 1, CurrentRoute = "Direct Route", CurrentSector = "Kepler Reach" };
            var roll = StarshipExpeditionStory.StableIndex(seed, 2, "event-type:Direct Route", 4);
            Assert.Equal(roll < 3 ? "alpha" : "beta", StarshipEventCatalog.Select(catalog, snapshot, 2).EventType);
            Assert.Equal(StarshipEventCatalog.Select(catalog, snapshot, 2), StarshipEventCatalog.Select(catalog.AsEnumerable().Reverse().ToList(), snapshot, 2));
        }
        Assert.Equal("beta", StarshipEventCatalog.Select(catalog, new() { CurrentLeg = 1, CurrentRoute = "Survey Route", CurrentSector = "Kepler Reach" }, 2).EventType);
        Assert.Equal("beta", StarshipEventCatalog.Select(catalog, new() { CurrentLeg = 1, CurrentRoute = "Direct Route", CurrentSector = "Vela Expanse" }, 2).EventType);
    }

    [Theory]
    [InlineData("duplicate-id")]
    [InlineData("duplicate-label")]
    [InlineData("continuing-default")]
    [InlineData("loop")]
    [InlineData("unknown-token")]
    [InlineData("command-text")]
    [InlineData("too-many")]
    [InlineData("risk")]
    [InlineData("bad-profile")]
    [InlineData("reveal-without-profile")]
    [InlineData("bad-image")]
    [InlineData("bad-sector")]
    [InlineData("bad-route")]
    [InlineData("bad-modifier")]
    public void Validation_RejectsUnsafeOrUnusableDefinitions(string defect)
    {
        var settings = new StarshipExpeditionSettings();
        var definition = Custom("custom-event");
        definition = defect switch
        {
            "duplicate-id" => definition with { EventType = "SignalDetected" },
            "duplicate-label" => definition with { Choices = [new() { ChoiceId = "one", Label = "Same" }, new() { ChoiceId = "two", Label = "same" }] },
            "continuing-default" => definition with { Choices = [new() { ChoiceId = "go", Label = "Go", ContinuesEvent = true, GrantsFlag = "done" }, new() { ChoiceId = "pass", Label = "Pass" }] },
            "loop" => definition with { Choices = [new() { ChoiceId = "go", Label = "Go", ContinuesEvent = true }, new() { ChoiceId = "pass", Label = "Pass" }] },
            "unknown-token" => definition with { Description = "An event with {money}" },
            "command-text" => definition with { Description = "!ban viewer" },
            "too-many" => definition with { Choices = Enumerable.Range(1, 7).Select(index => new StarshipEventChoiceSnapshot { ChoiceId = $"choice-{index}", Label = $"Choice {index}" }).ToList() },
            "risk" => definition with { Choices = [new() { ChoiceId = "go", Label = "Go", IsRisky = true }, new() { ChoiceId = "pass", Label = "Pass" }] },
            "bad-profile" => definition with { RewardProfileId = "missing" },
            "reveal-without-profile" => definition with { Choices = [new() { ChoiceId = "go", Label = "Go", RevealsRarity = true }, new() { ChoiceId = "pass", Label = "Pass" }] },
            "bad-image" => definition with { Images = ["javascript:alert(1)"] },
            "bad-sector" => definition with { AllowedSectors = ["Unknown Sector"] },
            "bad-route" => definition with { AllowedRoutes = ["Unknown Route"] },
            "bad-modifier" => definition with { Choices = [new() { ChoiceId = "go", Label = "Go", RewardMultiplierPercent = 301 }, new() { ChoiceId = "pass", Label = "Pass" }] },
            _ => definition
        };
        settings.EventCatalog.Add(definition);

        Assert.ThrowsAny<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
    }

    [Fact]
    public void Validation_RequiresRouteCoverageAndProtectsStructuralMechanics()
    {
        var settings = new StarshipExpeditionSettings();
        settings.EventCatalog = settings.EventCatalog.Select(definition => StarshipEventCatalog.IsStructural(definition.EventType) ? definition : definition with { Enabled = false }).ToList();
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
        settings.EventCatalog = StarshipEventCatalog.CreateDefaults();
        var routeIndex = settings.EventCatalog.FindIndex(definition => definition.EventType == "RouteChoice");
        settings.EventCatalog[routeIndex] = settings.EventCatalog[routeIndex] with { DefaultChoiceId = "detour" };
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
        settings.EventCatalog[routeIndex] = StarshipEventCatalog.CreateDefaults()[routeIndex] with { Title = "Pick a Journey", Images = ["/media/fork.png"] };
        StarshipExpeditionSettings.NormalizeAndValidate(settings);
    }

    [Fact]
    public void CustomContent_IsDeterministicAndUsesTierLootFallback()
    {
        var definition = Custom("archive") with
        {
            SubjectStyle = "custom", SubjectNames = ["the Lost Archive", "the Silver Vault"],
            ObjectKind = "beacon", Images = ["/media/archive.png"],
            LootByTier = new() { ["Legendary"] = "a sealed star atlas" }
        };
        var restored = JsonSerializer.Deserialize<StarshipEventDefinition>(JsonSerializer.Serialize(definition))!;
        Assert.Equal(StarshipExpeditionStory.GenerateSubject(81, 2, definition), StarshipExpeditionStory.GenerateSubject(81, 2, restored));
        var expeditionEvent = new StarshipExpeditionEventSnapshot { EventType = definition.EventType, LootByTier = definition.LootByTier };
        Assert.Equal("a sealed star atlas", StarshipExpeditionStory.Render("{loot}", expeditionEvent, 81, [], "Legendary"));
        Assert.Equal("a common find", StarshipExpeditionStory.Render("{loot}", expeditionEvent, 81, [], "Common"));
    }

    [Fact]
    public void DisabledBuiltInSector_RemainsValidFilterButCannotSelectItsEvent()
    {
        var settings = new StarshipExpeditionSettings { DisabledBuiltInSectors = ["Kepler Reach"] };
        settings.EventCatalog.Add(Custom("kepler-only") with { AllowedSectors = ["Kepler Reach"], Weight = 10 });
        StarshipExpeditionSettings.NormalizeAndValidate(settings);

        foreach (var sector in settings.GetEffectiveSectors())
        {
            var selected = StarshipEventCatalog.Select(settings.EventCatalog,
                new() { CurrentLeg = 1, CurrentRoute = "Uncharted", CurrentSector = sector, Seed = 15 }, 2);
            Assert.NotEqual("kepler-only", selected.EventType);
        }
    }

    [Fact]
    public void ShipEffects_ValidateBoundsAffordableDefaultAndProtectedStationMechanics()
    {
        var settings = new StarshipExpeditionSettings();
        var definition = Custom("ship-cache") with
        {
            SuccessChancePercent = 75,
            ShipSuccessEffect = new() { SuppliesDelta = 10 },
            ShipFailureEffect = new() { HullDelta = -20 }
        };
        settings.EventCatalog.Add(definition);
        StarshipExpeditionSettings.NormalizeAndValidate(settings);
        var index = settings.EventCatalog.Count - 1;
        settings.EventCatalog[index] = definition with { ShipFailureEffect = new() { HullDelta = -101 } };
        Assert.Throws<ArgumentOutOfRangeException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
        settings.EventCatalog[index] = definition with { ShipFailureEffect = new() { SuppliesCost = 1 } };
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
        settings.EventCatalog[index] = definition with
        {
            Choices = [new() { ChoiceId = "go", Label = "Go", ShipEffect = new() { SuppliesCost = 1 } }, new() { ChoiceId = "pass", Label = "Pass" }]
        };
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
        settings.EventCatalog[index] = definition;
        var stationIndex = settings.EventCatalog.FindIndex(item => item.EventType == "StationStop");
        settings.EventCatalog[stationIndex] = settings.EventCatalog[stationIndex] with { ShipSuccessEffect = new() { HullDelta = 1 } };
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
    }

    private static StarshipEventDefinition Custom(string id) => new(id, id, "An event near {subject}.",
        [new() { ChoiceId = "go", Label = "Go" }, new() { ChoiceId = "pass", Label = "Pass", EndsWithoutReward = true }], "go");
}