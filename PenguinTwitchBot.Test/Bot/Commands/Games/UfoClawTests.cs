using System.Text.Json;
using PenguinTwitchBot.Bot.Commands.Games.UfoClaw;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Overlay;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace PenguinTwitchBot.Test.Bot.Commands.Games;

public class UfoClawTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.249, 0)]
    [InlineData(0.25, 2)]
    [InlineData(0.999, 2)]
    public void WeightedDrawHonorsBoundariesAndExclusions(double sample, int expected)
    {
        UfoClawEntry[] entries = [new() { Weight = 0.25m }, new() { Weight = 100, Enabled = false }, new() { Weight = 0.75m }, new() { Weight = 0 }];
        Assert.Equal(expected, UfoClawDraw.Select(entries, (decimal)sample));
    }

    [Fact]
    public void EmptyAndInvalidPoolsAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => UfoClawDraw.Select([], 0));
        Assert.Throws<InvalidOperationException>(() => UfoClawDraw.Select([new() { Weight = 0 }], 0));
        Assert.Throws<ArgumentException>(() => UfoClawDraw.Select([new() { Weight = -1 }], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => UfoClawDraw.Select([new()], 1));
    }

    private sealed class MemoryStore : IUfoClawStore
    {
        public UfoClawDocument Document { get; private set; } = new();
        public bool FailSave { get; set; }
        public Task<UfoClawDocument> LoadAsync() => Task.FromResult(UfoClawStore.Copy(Document));
        public Task SaveAsync(UfoClawDocument document)
        {
            if (FailSave) throw new IOException("Simulated failure");
            UfoClawStore.Validate(document);
            Document = UfoClawStore.Copy(document);
            return Task.CompletedTask;
        }
    }

    private static UfoClawService CreateService(MemoryStore store, FakeTimeProvider clock, IDefaultCommandTriggerService? triggers = null) =>
        new(Mock.Of<IServiceBackbone>(), Mock.Of<ICommandHandler>(), Mock.Of<IPenguinDispatcher>(), store,
            Mock.Of<IWebSocketMessenger>(), triggers ?? Mock.Of<IDefaultCommandTriggerService>(), NullLogger<UfoClawService>.Instance, clock);

    private static UfoClawSettings Settings() => new()
    {
        DurationSeconds = 11,
        RemoveWinners = true,
        PrizeSets = [new() { Id = "set", Name = "Test prizes", Entries = [new() { Id = "prize", Label = "Winner", Value = "25" }] }]
    };

    [Fact]
    public async Task HostedServiceStartsHiddenEvenIfPreviousSessionLeftItVisible()
    {
        var store = new MemoryStore();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var previousSession = CreateService(store, clock);
        await previousSession.SaveSettingsAsync(Settings());
        await previousSession.ControlAsync("show", "prizes", "set");
        Assert.True(store.Document.Visible);

        var commands = new Mock<ICommandHandler>();
        commands.Setup(handler => handler.GetDefaultCommandFromDb(UfoClawService.CommandName)).ReturnsAsync((DefaultCommand?)null);
        commands.Setup(handler => handler.AddDefaultCommand(It.IsAny<DefaultCommand>())).ReturnsAsync((DefaultCommand command) => command);
        var service = new UfoClawService(Mock.Of<IServiceBackbone>(), commands.Object, Mock.Of<IPenguinDispatcher>(), store,
            Mock.Of<IWebSocketMessenger>(), Mock.Of<IDefaultCommandTriggerService>(), NullLogger<UfoClawService>.Instance, clock);

        await service.StartAsync(CancellationToken.None);
        Assert.False((await service.GetAsync()).Visible);
        Assert.False(store.Document.Visible);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task OnCommandFiltersNonJoinCommandsAndReportsViewerPoolValidationErrors()
    {
        var store = new MemoryStore();
        store.Document.Settings = Settings();
        store.Document.Mode = "viewers";
        store.Document.EntriesOpen = true;
        store.Document.Viewers = Enumerable.Range(0, 10000)
            .Select(index => new UfoClawEntry { Id = $"viewer-{index}", UserId = $"viewer-{index}", Login = $"viewer{index}", Label = $"Viewer {index}" })
            .ToList();
        var backbone = new Mock<IServiceBackbone>();
        backbone.Setup(item => item.SendChatMessage("Viewer", It.IsAny<string>(), true)).Returns(Task.CompletedTask);
        var commands = new Mock<ICommandHandler>();
        commands.Setup(item => item.GetCommandDefaultName("other")).Returns("other");
        commands.Setup(item => item.GetCommandDefaultName("joinclaw")).Returns(UfoClawService.CommandName);
        var service = new UfoClawService(backbone.Object, commands.Object, Mock.Of<IPenguinDispatcher>(), store,
            Mock.Of<IWebSocketMessenger>(), Mock.Of<IDefaultCommandTriggerService>(), NullLogger<UfoClawService>.Instance,
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        await service.OnCommand(null, new() { Command = "other", UserId = "new-viewer", Name = "viewer", DisplayName = "Viewer" });
        Assert.Equal(10000, store.Document.Viewers.Count);
        await service.OnCommand(null, new() { Command = "joinclaw", UserId = "new-viewer", Name = "viewer", DisplayName = "Viewer" });
        backbone.Verify(item => item.SendChatMessage("Viewer", It.Is<string>(message => message.Contains("maximum 10,000")), true), Times.Once);
        Assert.Equal(10000, store.Document.Viewers.Count);
    }

    [Fact]
    public async Task HiddenGrabSettlesOnceAfterRecoveryAndPreservesFrozenResultVariables()
    {
        var store = new MemoryStore();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var triggers = new Mock<IDefaultCommandTriggerService>();
        var service = CreateService(store, clock, triggers.Object);
        await service.SaveSettingsAsync(Settings());
        await service.ControlAsync("show", "prizes", "set");
        await service.ControlAsync("grab");
        var id = (await service.GetAsync()).Grab!.Id;
        await service.ControlAsync("hide");
        var changed = Settings();
        changed.PrizeSets[0].Entries[0].Label = "Edited later";
        changed.RemoveWinners = false;
        await service.SaveSettingsAsync(changed);
        var recovered = CreateService(store, clock, triggers.Object);
        clock.Advance(TimeSpan.FromSeconds(11));
        Assert.True(await recovered.AcknowledgeAnimationAsync(id));
        await recovered.TickAsync();
        await recovered.TickAsync();
        var document = await recovered.GetAsync();
        Assert.False(document.Visible);
        Assert.True(document.Grab!.Completed);
        Assert.Equal(id, document.Grab.Id);
        Assert.Contains("prize", document.Removed["set"]);
        var variables = UfoClawService.ResultVariables(document.Grab);
        Assert.Equal("Winner", variables["WinningLabel"]);
        Assert.Equal("25", variables["WinningValue"]);
        Assert.Equal("true", variables["RemovedAfterWin"]);
        triggers.Verify(trigger => trigger.TriggerDefaultCommandEventAsync(UfoClawService.CommandName, DefaultCommandEventTypes.UfoClawResult,
            It.IsAny<CommandEventArgs>(), It.IsAny<Dictionary<string, string>>()), Times.Once);
    }

    [Fact]
    public async Task ResultCompletesAtAcknowledgementOrHideWithoutFallback()
    {
        var store = new MemoryStore();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(store, clock);
        await service.SaveSettingsAsync(Settings());
        await service.ControlAsync("show", "prizes", "set");
        await service.ControlAsync("grab");
        var grabId = (await service.GetAsync()).Grab!.Id;
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(await service.AcknowledgeAnimationAsync(Guid.NewGuid()));
        Assert.True(await service.AcknowledgeAnimationAsync(grabId));
        await service.TickAsync();
        var completed = (await service.GetAsync()).Grab!;
        Assert.True(completed.Completed);
        Assert.Equal(clock.GetUtcNow(), completed.CompletedAtUtc);

        var hiddenStore = new MemoryStore();
        var hiddenClock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var hiddenService = CreateService(hiddenStore, hiddenClock);
        await hiddenService.SaveSettingsAsync(Settings());
        await hiddenService.ControlAsync("show", "prizes", "set");
        await hiddenService.ControlAsync("grab");
        hiddenClock.Advance(TimeSpan.FromMinutes(5));
        await hiddenService.TickAsync();
        Assert.False((await hiddenService.GetAsync()).Grab!.Completed);
        await hiddenService.ControlAsync("hide");
        Assert.True((await hiddenService.GetAsync()).Grab!.Completed);
    }

    [Fact]
    public async Task NewGrabSupersedesUnacknowledgedGrabAndResetsConfirmation()
    {
        var store = new MemoryStore();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(store, clock);
        await service.SaveSettingsAsync(Settings());
        await service.ControlAsync("show", "prizes", "set");
        await service.ControlAsync("grab");
        var first = (await service.GetAsync()).Grab!;
        clock.Advance(TimeSpan.FromMinutes(5));
        await service.ControlAsync("grab");
        var next = (await service.GetAsync()).Grab!;
        Assert.NotEqual(first.Id, next.Id);
        Assert.False(next.OverlayAnimationAcknowledged);
        Assert.False(next.Completed);
    }

    [Fact]
    public async Task OverlayCompletionEndpointRequiresSharedSecurityKey()
    {
        var store = new MemoryStore();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(store, clock);
        await service.SaveSettingsAsync(Settings());
        await service.ControlAsync("show", "prizes", "set");
        await service.ControlAsync("grab");
        var grabId = (await service.GetAsync()).Grab!.Id;
        clock.Advance(TimeSpan.FromSeconds(11));
        var security = new Mock<IOverlaySecurityService>();
        security.Setup(item => item.ValidateKeyAsync("valid-overlay-key")).ReturnsAsync(true);
        security.Setup(item => item.ValidateKeyAsync("invalid-overlay-key")).ReturnsAsync(false);
        var controller = new UfoClawOverlayController(service, security.Object);

        Assert.IsType<UnauthorizedResult>(await controller.Complete(new(grabId, "invalid-overlay-key")));
        Assert.False((await service.GetAsync()).Grab!.OverlayAnimationAcknowledged);
        Assert.IsType<AcceptedResult>(await controller.Complete(new(grabId, "valid-overlay-key")));
        Assert.True((await service.GetAsync()).Grab!.OverlayAnimationAcknowledged);
    }

    [Fact]
    public async Task AppearanceSettingsUpdateDuringGrabWithoutChangingFrozenDrawRules()
    {
        var store = new MemoryStore();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(store, clock);
        await service.SaveSettingsAsync(Settings());
        await service.ControlAsync("show", "prizes", "set");
        await service.ControlAsync("grab");
        var updated = (await service.GetAsync()).Settings;
        updated.LeftSafeGapPixels = 260;
        updated.RightSafeGapPixels = 180;
        updated.PitBackgroundEnabled = false;
        updated.DurationSeconds = 30;
        await service.SaveSettingsAsync(updated);
        var snapshot = JsonSerializer.SerializeToElement(await service.GetOverlayAsync(), UfoClawStore.JsonOptions);
        var overlaySettings = snapshot.GetProperty("settings");
        Assert.Equal(260, overlaySettings.GetProperty("leftSafeGapPixels").GetInt32());
        Assert.Equal(180, overlaySettings.GetProperty("rightSafeGapPixels").GetInt32());
        Assert.False(overlaySettings.GetProperty("pitBackgroundEnabled").GetBoolean());
        Assert.Equal(11, overlaySettings.GetProperty("durationSeconds").GetDouble());
    }

    [Fact]
    public async Task ViewerEnrollmentDeduplicatesAndRemovedWinnerCannotRejoinUntilReset()
    {
        var store = new MemoryStore();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(store, clock);
        await service.SaveSettingsAsync(Settings());
        await service.ControlAsync("open");
        var viewer = new CommandEventArgs { UserId = "123", Name = "viewer", DisplayName = "Viewer" };
        await service.JoinAsync(viewer);
        await service.JoinAsync(viewer);
        Assert.Single((await service.GetAsync()).Viewers);
        await service.ControlAsync("grab");
        var grabId = (await service.GetAsync()).Grab!.Id;
        await service.JoinAsync(new() { UserId = "456", Name = "other", DisplayName = "Other" });
        Assert.Single((await service.GetAsync()).Grab!.Entries);
        Assert.True(await service.AcknowledgeAnimationAsync(grabId));
        await service.JoinAsync(viewer);
        Assert.DoesNotContain(UfoClawService.Eligible(await service.GetAsync()), entry => entry.UserId == "123");
        clock.Advance(TimeSpan.FromSeconds(4));
        await service.ControlAsync("reset");
        Assert.Equal(2, UfoClawService.Eligible(await service.GetAsync()).Count);
        await service.ControlAsync("close");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.JoinAsync(viewer));
    }

    [Fact]
    public async Task FailedSaveDoesNotAcceptGrabAndExhaustionRequiresExplicitReset()
    {
        var store = new MemoryStore();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(store, clock);
        await service.SaveSettingsAsync(Settings());
        await service.ControlAsync("show", "prizes", "set");
        store.FailSave = true;
        await Assert.ThrowsAsync<IOException>(() => service.ControlAsync("grab"));
        Assert.Null((await service.GetAsync()).Grab);
        store.FailSave = false;
        await service.ControlAsync("grab");
        var grabId = (await service.GetAsync()).Grab!.Id;
        Assert.True(await service.AcknowledgeAnimationAsync(grabId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ControlAsync("grab"));
        clock.Advance(TimeSpan.FromSeconds(4));
        await service.ControlAsync("reset");
        await service.ControlAsync("grab");
        Assert.NotNull((await service.GetAsync()).Grab);
    }
}