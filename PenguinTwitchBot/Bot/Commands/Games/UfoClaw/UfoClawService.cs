using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;

namespace PenguinTwitchBot.Bot.Commands.Games.UfoClaw;

public interface IUfoClawService
{
    Task<UfoClawDocument> GetAsync();
    Task<object> GetOverlayAsync();
    Task<bool> AcknowledgeAnimationAsync(Guid grabId);
    Task SaveSettingsAsync(UfoClawSettings settings);
    Task ControlAsync(string operation, string mode = "prizes", string prizeSetId = "", CommandEventArgs? caller = null);
}

public sealed class UfoClawService(
    IServiceBackbone backbone, ICommandHandler commands, IPenguinDispatcher dispatcher,
    IUfoClawStore store, IWebSocketMessenger messenger, IDefaultCommandTriggerService triggers,
    ILogger<UfoClawService> logger, TimeProvider? timeProvider = null)
    : BaseCommandService(backbone, commands, "UfoClaw", dispatcher), IUfoClawService, IHostedService
{
    public const string CommandName = "joinclaw";
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private UfoClawDocument? _document;
    private CancellationTokenSource? _cancellation;
    private Task? _scheduler;
    private bool _active = true;

    public override Task Register() => RegisterDefaultCommand(CommandName, this, ModuleName, Rank.Viewer, userCooldown: 2,
        description: "Join the open UFO claw viewer draw.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _active = true;
        await Register();
        await GetAsync();
        await ControlAsync("hide");
        _cancellation = new CancellationTokenSource();
        _scheduler = ScheduleAsync(_cancellation.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _active = false;
        if (_cancellation != null)
        {
            await _cancellation.CancelAsync();
            if (_scheduler != null) await _scheduler.WaitAsync(cancellationToken);
            _cancellation.Dispose();
            _cancellation = null;
        }
        await BroadcastAsync(await GetAsync());
    }

    private async Task ScheduleAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250), _clock);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try { await TickAsync(); }
                catch (Exception exception) { logger.LogError(exception, "UFO claw completion failed; will retry state processing."); }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    public async Task<UfoClawDocument> GetAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _document ??= await store.LoadAsync();
            var copy = UfoClawStore.Copy(_document);
            copy.Visible &= _active && copy.Settings.Enabled;
            return copy;
        }
        finally { _lock.Release(); }
    }

    public async Task<object> GetOverlayAsync() => OverlayState(await GetAsync());

    public async Task<bool> AcknowledgeAnimationAsync(Guid grabId)
    {
        UfoClawDocument? updated = null;
        await _lock.WaitAsync();
        try
        {
            _document ??= await store.LoadAsync();
            if (_document.Grab?.Id != grabId) return false;
            if (_document.Grab.OverlayAnimationAcknowledged || _document.Grab.Completed) return true;
            var next = UfoClawStore.Copy(_document);
            next.Grab!.OverlayAnimationAcknowledged = true;
            next.Grab.OverlayAnimationAcknowledgedAtUtc = _clock.GetUtcNow();
            next.Revision++;
            await store.SaveAsync(next);
            _document = updated = next;
        }
        finally { _lock.Release(); }
        if (updated != null) await BroadcastAsync(updated);
        if (updated != null) await TickAsync();
        return true;
    }

    private bool Busy(UfoClawDocument document)
    {
        if (document.Grab is not { } grab) return false;
        var now = _clock.GetUtcNow();
        var animationEnd = grab.StartedAtUtc.AddSeconds(grab.Rules.DurationSeconds);
        var revealEnd = (grab.CompletedAtUtc ?? animationEnd).AddSeconds(grab.Rules.RevealSeconds);
        if (grab.Completed) return now < revealEnd;
        if (grab.OverlayAnimationAcknowledged)
            return now < (grab.OverlayAnimationAcknowledgedAtUtc ?? animationEnd).AddSeconds(grab.Rules.RevealSeconds);
        return false;
    }

    public static List<UfoClawEntry> Eligible(UfoClawDocument document)
    {
        var key = document.Mode == "viewers" ? "viewers" : document.PrizeSetId;
        var entries = document.Mode == "viewers" ? document.Viewers : document.Settings.PrizeSets.FirstOrDefault(set => set.Id == key)?.Entries ?? [];
        document.Removed.TryGetValue(key, out var removed);
        return entries.Where(entry => entry.Enabled && entry.Weight > 0 && removed?.Contains(entry.Id) != true).ToList();
    }

    private async Task MutateAsync(Action<UfoClawDocument> mutation)
    {
        UfoClawDocument next;
        await _lock.WaitAsync();
        try
        {
            _document ??= await store.LoadAsync();
            next = UfoClawStore.Copy(_document);
            mutation(next);
            next.Revision++;
            await store.SaveAsync(next);
            _document = next;
        }
        finally { _lock.Release(); }
        await BroadcastAsync(next);
    }

    public Task SaveSettingsAsync(UfoClawSettings settings)
    {
        UfoClawStore.ValidateSettings(settings);
        return MutateAsync(document => document.Settings = UfoClawStore.Copy(settings));
    }

    public async Task ControlAsync(string operation, string mode = "prizes", string prizeSetId = "", CommandEventArgs? caller = null)
    {
        if (!_active) throw new InvalidOperationException("UFO Claw is disabled.");
        await TickAsync();
        await MutateAsync(document =>
        {
            if (!document.Settings.Enabled && operation != "hide") throw new InvalidOperationException("UFO Claw is disabled in settings.");
            if (operation is "open" or "reset" && Busy(document)) throw new InvalidOperationException("The claw is busy.");
            switch (operation)
            {
                case "show":
                    if (Busy(document))
                    {
                        if (mode != document.Mode || (mode == "prizes" && prizeSetId != document.PrizeSetId)) throw new InvalidOperationException("The claw is busy.");
                        document.Visible = true;
                        break;
                    }
                    if (mode is not ("prizes" or "viewers")) throw new ArgumentException("Select prizes or viewers.");
                    if (mode == "prizes" && !document.Settings.PrizeSets.Any(set => set.Id == prizeSetId)) throw new ArgumentException("Select an existing prize set.");
                    document.Mode = mode;
                    document.PrizeSetId = prizeSetId;
                    document.Grab = null;
                    document.Visible = true;
                    break;
                case "hide":
                    document.Visible = false;
                    if (document.Grab is { Completed: false, OverlayAnimationAcknowledged: false } grab)
                    {
                        grab.OverlayAnimationAcknowledged = true;
                        grab.OverlayAnimationAcknowledgedAtUtc = _clock.GetUtcNow();
                    }
                    break;
                case "open":
                    document.Viewers.Clear();
                    document.Removed.Remove("viewers");
                    document.EntriesOpen = true;
                    document.Mode = "viewers";
                    document.Grab = null;
                    document.Visible = true;
                    break;
                case "close": document.EntriesOpen = false; break;
                case "reset": document.Removed.Remove(document.Mode == "viewers" ? "viewers" : document.PrizeSetId); document.Grab = null; break;
                case "grab":
                    if (Busy(document)) throw new InvalidOperationException("The claw is busy.");
                    var entries = Eligible(document);
                    var index = UfoClawDraw.Select(entries, RandomNumberGenerator.GetInt32(int.MaxValue) / (decimal)int.MaxValue);
                    document.Grab = new UfoClawGrab
                    {
                        StartedAtUtc = _clock.GetUtcNow(), Mode = document.Mode, PrizeSetId = document.PrizeSetId,
                        PrizeSetName = document.Mode == "viewers" ? "Viewers" : document.Settings.PrizeSets.First(set => set.Id == document.PrizeSetId).Name,
                        Entries = UfoClawStore.Copy(entries), WinningIndex = index,
                        Rules = UfoClawStore.Copy(document.Settings) with { PrizeSets = [] },
                        CallerUserId = caller?.UserId ?? "", CallerLogin = caller?.Name ?? "system", CallerDisplayName = caller?.DisplayName ?? "System"
                    };
                    document.Visible = true;
                    break;
                default: throw new ArgumentException("Unknown UFO claw operation.");
            }
        });
        if (operation == "hide") await TickAsync();
    }

    public override async Task OnCommand(object? sender, CommandEventArgs eventArgs)
    {
        if (!_active || ServiceBackbone.IsKnownBot(eventArgs.Name) || string.IsNullOrWhiteSpace(eventArgs.UserId)) return;
        try
        {
            await JoinAsync(eventArgs);
        }
        catch (InvalidOperationException exception) { await RespondWithMessage(eventArgs, exception.Message); }
    }

    public Task JoinAsync(CommandEventArgs viewer) => MutateAsync(document =>
    {
        if (!_active || !document.Settings.Enabled || !document.EntriesOpen) throw new InvalidOperationException("UFO claw entries are closed.");
        if (ServiceBackbone.IsKnownBot(viewer.Name) || string.IsNullOrWhiteSpace(viewer.UserId)) throw new InvalidOperationException("A viewer identity is required.");
        if (document.Viewers.Any(entry => entry.UserId == viewer.UserId)) return;
        document.Viewers.Add(new UfoClawEntry { Id = viewer.UserId, UserId = viewer.UserId, Login = viewer.Name, Label = viewer.DisplayName, Weight = 1 });
    });

    public async Task TickAsync()
    {
        UfoClawGrab? resolved = null;
        UfoClawDocument? updated = null;
        await _lock.WaitAsync();
        try
        {
            _document ??= await store.LoadAsync();
            var next = UfoClawStore.Copy(_document);
            if (next.Grab is not { } grab) return;
            if (!grab.Completed && grab.OverlayAnimationAcknowledged)
            {
                grab.Completed = true;
                grab.CompletedAtUtc = _clock.GetUtcNow();
                grab.DispatchClaimed = true;
                if (grab.Rules.RemoveWinners)
                {
                    var key = grab.Mode == "viewers" ? "viewers" : grab.PrizeSetId;
                    if (!next.Removed.TryGetValue(key, out var removed)) next.Removed[key] = removed = [];
                    removed.Add(grab.Winner.Id);
                }
                resolved = grab;
            }
            var revealHoldStart = grab.OverlayAnimationAcknowledgedAtUtc ?? grab.CompletedAtUtc ??
                grab.StartedAtUtc.AddSeconds(grab.Rules.DurationSeconds);
            if (grab.Completed && grab.Rules.AutoHide && _clock.GetUtcNow() >= revealHoldStart.AddSeconds(grab.Rules.RevealSeconds) && next.Visible)
                next.Visible = false;
            if (resolved == null && next.Visible == _document.Visible) return;
            next.Revision++;
            await store.SaveAsync(next);
            _document = updated = next;
        }
        finally { _lock.Release(); }
        if (updated != null) await BroadcastAsync(updated);
        if (resolved != null)
        {
            try
            {
                var winner = resolved.Winner;
                var viewerMode = resolved.Mode == "viewers";
                await triggers.TriggerDefaultCommandEventAsync(CommandName, DefaultCommandEventTypes.UfoClawResult, new CommandEventArgs
                {
                    Command = CommandName, Arg = winner.Label, Args = [winner.Label],
                    UserId = viewerMode ? winner.UserId ?? "" : resolved.CallerUserId,
                    Name = viewerMode ? winner.Login ?? "" : resolved.CallerLogin,
                    DisplayName = viewerMode ? winner.Label : resolved.CallerDisplayName, FromOwnChannel = true
                }, ResultVariables(resolved));
            }
            catch (Exception exception) { logger.LogError(exception, "UFO claw {GrabId} result dispatch failed after its persistent claim; manual review required.", resolved.Id); }
        }
    }

    public static Dictionary<string, string> ResultVariables(UfoClawGrab grab) => new()
    {
        ["GrabId"] = grab.Id.ToString(), ["OperationId"] = grab.Id.ToString(), ["Mode"] = grab.Mode,
        ["UfoClawResult"] = grab.Winner.Label, ["WinningLabel"] = grab.Winner.Label, ["WinningValue"] = grab.Winner.Value,
        ["WinningEntryId"] = grab.Winner.Id, ["WinningIndex"] = grab.WinningIndex.ToString(CultureInfo.InvariantCulture),
        ["PrizeSetId"] = grab.PrizeSetId, ["PrizeSetName"] = grab.PrizeSetName,
        ["WinningUserId"] = grab.Winner.UserId ?? "", ["WinningLogin"] = grab.Winner.Login ?? "",
        ["WinningDisplayName"] = grab.Mode == "viewers" ? grab.Winner.Label : "", ["IsViewerDraw"] = (grab.Mode == "viewers").ToString().ToLowerInvariant(),
        ["EntriesCount"] = grab.Entries.Count.ToString(CultureInfo.InvariantCulture), ["RemovedAfterWin"] = grab.Rules.RemoveWinners.ToString().ToLowerInvariant(),
        ["WinningMessage"] = $"The claw chose {grab.Winner.Label}!"
    };

    public object OverlayState(UfoClawDocument document) => new
    {
        type = "ufo_state", revision = document.Revision, serverNowUtc = _clock.GetUtcNow(), visible = document.Visible && _active && document.Settings.Enabled,
        entriesOpen = document.EntriesOpen, mode = document.Mode,
        entries = document.Grab?.Entries ?? Eligible(document),
        settings = document.Grab is { } grab
            ? grab.Rules with
            {
                PitBackgroundEnabled = document.Settings.PitBackgroundEnabled,
                LeftSafeGapPixels = document.Settings.LeftSafeGapPixels,
                RightSafeGapPixels = document.Settings.RightSafeGapPixels
            }
            : document.Settings with { PrizeSets = [] },
        grab = document.Grab == null ? null : new { document.Grab.Id, document.Grab.StartedAtUtc, document.Grab.WinningIndex, document.Grab.Completed }
    };

    private async Task BroadcastAsync(UfoClawDocument document)
    {
        try { await messenger.AddToQueue(WsTopics.UfoClaw, JsonSerializer.Serialize(OverlayState(document), UfoClawStore.JsonOptions)); }
        catch (Exception exception) { logger.LogError(exception, "Unable to broadcast UFO claw state; clients can recover from the snapshot."); }
    }
}