using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Application.Notifications;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed class StarshipExpeditionService(
    IServiceBackbone serviceBackbone,
    ICommandHandler commandHandler,
    IStarshipExpeditionStateStore stateStore,
    IStarshipExpeditionSettingsStore settingsStore,
    IWebSocketMessenger webSocketMessenger,
    ILogger<StarshipExpeditionService> logger,
    IPenguinDispatcher dispatcher,
    TimeProvider? timeProvider = null,
    IDefaultCommandTriggerService? defaultCommandTriggerService = null)
    : BaseCommandService(serviceBackbone, commandHandler, "StarshipExpedition", dispatcher), IHostedService, IStarshipExpeditionService
{
    public const string DefaultCommandName = "starship";
    public const int DefaultLegCount = 5;
    public const int DefaultMinimumEventIntervalMinutes = 15;
    public const int DefaultMaximumEventIntervalMinutes = 30;
    public const int DefaultStationIntervalLegs = 2;
    public const int DefaultVoteDurationSeconds = 60;
    public const int DefaultBoardingDurationSeconds = 60;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly EventDefinition[] RandomEvents =
    [
        new("SignalDetected", "Unidentified Signal", "A signal is repeating from the edge of the sensor range.",
            [new() { ChoiceId = "scan", Label = "Scan the signal" }, new() { ChoiceId = "trace", Label = "Trace its origin" }, new() { ChoiceId = "ignore", Label = "Continue on course" }], "ignore"),
        new("SalvageSite", "Derelict Salvage", "A damaged vessel is drifting near the route.",
            [new() { ChoiceId = "salvage", Label = "Recover salvage" }, new() { ChoiceId = "secure", Label = "Secure the area first" }, new() { ChoiceId = "pass", Label = "Pass it by" }], "pass"),
        new("Hazard", "Navigation Hazard", "Debris has appeared across the current flight path.",
            [new() { ChoiceId = "maneuver", Label = "Maneuver around it" }, new() { ChoiceId = "shield", Label = "Raise shields" }, new() { ChoiceId = "hold", Label = "Hold course" }], "maneuver")
    ];

    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private StarshipExpeditionSnapshot? _snapshot;
    private bool _hasLoaded;
    private CancellationTokenSource? _schedulerCancellation;
    private Task? _schedulerTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Register();
        await EnsureLoadedAsync(cancellationToken);
        _schedulerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _schedulerTask = RunSchedulerAsync(_schedulerCancellation.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_schedulerCancellation == null) return;

        await _schedulerCancellation.CancelAsync();
        if (_schedulerTask != null)
        {
            try
            {
                await _schedulerTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _schedulerCancellation.Dispose();
        _schedulerCancellation = null;
        _schedulerTask = null;
    }

    public override async Task Register()
    {
        await RegisterDefaultCommand(
            DefaultCommandName,
            this,
            ModuleName,
            Rank.Viewer,
            userCooldown: 2,
            description: "Join or vote in the shared Starship Expedition; moderators can start, stop, and advance it.");
    }

    public override async Task OnCommand(object? sender, CommandEventArgs eventArgs)
    {
        var args = eventArgs.Args.Count > 0
            ? eventArgs.Args
            : (eventArgs.Arg ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var subCommand = args.FirstOrDefault()?.Trim().ToLowerInvariant();

        try
        {
            switch (subCommand)
            {
                case "start":
                    if (!eventArgs.IsModOrHigher())
                    {
                        await RespondWithMessage(eventArgs, "Only a moderator or broadcaster can start the Starship Expedition.");
                        return;
                    }

                    var legCount = ParseOptionalInteger(args, 1);
                    var minimumInterval = ParseOptionalInteger(args, 2);
                    var maximumInterval = ParseOptionalInteger(args, 3);
                    var started = await StartExpeditionAsync(eventArgs.DisplayName, legCount, minimumInterval, maximumInterval);
                    await RespondWithMessage(eventArgs, $"Starship Expedition boarding is open for {started.BoardingDurationSeconds} seconds.");
                    return;

                case "stop":
                    if (!eventArgs.IsModOrHigher())
                    {
                        await RespondWithMessage(eventArgs, "Only a moderator or broadcaster can stop the Starship Expedition.");
                        return;
                    }

                    var stopped = await StopExpeditionAsync(eventArgs.DisplayName);
                    await RespondWithMessage(eventArgs, stopped == null ? "No Starship Expedition is active." : "Starship Expedition aborted.");
                    return;

                case "advance":
                    if (!eventArgs.IsModOrHigher())
                    {
                        await RespondWithMessage(eventArgs, "Only a moderator or broadcaster can advance the Starship Expedition.");
                        return;
                    }

                    var advanced = await AdvanceEventAsync();
                    await RespondWithMessage(eventArgs, FormatStatus(advanced));
                    return;

                case "join":
                    await JoinCrewAsync(eventArgs);
                    await RespondWithMessage(eventArgs, "You joined the Starship Expedition crew.");
                    return;

                case "vote":
                    if (args.Count < 2)
                    {
                        await RespondWithMessage(eventArgs, "Choose one of the current Starship event options: !starship vote <choice>");
                        return;
                    }

                    await VoteAsync(eventArgs, args[1]);
                    await RespondWithMessage(eventArgs, "Your Starship event choice was recorded.");
                    return;

                case "status":
                    await RespondWithMessage(eventArgs, FormatStatus(await GetSnapshotAsync()));
                    return;

                default:
                    await RespondWithMessage(eventArgs, "Use !starship join or !starship vote <choice>. Moderators: !starship start [legs] [min minutes] [max minutes], !starship advance, or !starship stop.");
                    return;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            await RespondWithMessage(eventArgs, ex.Message);
        }
    }

    public async Task<StarshipExpeditionSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            return _snapshot == null ? null : Clone(_snapshot);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public IReadOnlyList<StarshipExpeditionEventSnapshot> BuildPreview(StarshipExpeditionSettings settings, int seed)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var previewSettings = StarshipExpeditionSettings.NormalizeAndValidate(
            JsonSerializer.Deserialize<StarshipExpeditionSettings>(JsonSerializer.Serialize(settings, JsonOptions), JsonOptions)
            ?? throw new InvalidOperationException("Could not copy Starship Expedition preview settings."));
        var sectors = previewSettings.GetEffectiveSectors();
        var start = DateTimeOffset.UnixEpoch;
        var previewState = new StarshipExpeditionSnapshot
        {
            ExpeditionId = CreateDeterministicPreviewId(seed),
            Seed = seed,
            StartedBy = "Preview",
            StartedAtUtc = start,
            UpdatedAtUtc = start,
            LegCount = previewSettings.LegCount,
            StationIntervalLegs = previewSettings.StationIntervalLegs,
            MinimumEventIntervalSeconds = previewSettings.MinimumEventIntervalMinutes * 60,
            MaximumEventIntervalSeconds = previewSettings.MaximumEventIntervalMinutes * 60,
            EventVoteDurationSeconds = previewSettings.EventVoteDurationSeconds,
            ActiveCrewRewardsOnly = previewSettings.ActiveCrewRewardsOnly,
            SectorPool = sectors,
            CurrentSector = sectors[GetDeterministicIndex(seed, 0, "initial-sector", sectors.Count)],
            CurrentRoute = "Uncharted"
        };

        var events = new List<StarshipExpeditionEventSnapshot>(previewSettings.LegCount);
        OpenNextEventLocked(previewState, start);
        while (previewState.CurrentEvent is { } currentEvent)
        {
            var previewEvent = Clone(currentEvent);
            previewEvent.ResolvedChoiceId = previewEvent.DefaultChoiceId;
            previewEvent.ResolvedAtUtc = previewEvent.ClosesAtUtc;
            events.Add(previewEvent);

            ResolveCurrentEventLocked(previewState, previewEvent.ResolvedAtUtc.Value);
            if (previewState.Status != StarshipExpeditionStatus.Cruising) break;
            OpenNextEventLocked(previewState, previewState.NextEventAtUtc ?? previewEvent.ResolvedAtUtc.Value);
        }

        return events;
    }

    public async Task<StarshipExpeditionSnapshot> StartExpeditionAsync(
        string? startedBy,
        int? legCount = null,
        int? minimumEventIntervalMinutes = null,
        int? maximumEventIntervalMinutes = null,
        int? seed = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        var effectiveLegCount = legCount ?? settings.LegCount;
        var effectiveMinimumInterval = minimumEventIntervalMinutes ?? settings.MinimumEventIntervalMinutes;
        var effectiveMaximumInterval = maximumEventIntervalMinutes ?? settings.MaximumEventIntervalMinutes;
        ValidateStartOptions(effectiveLegCount, effectiveMinimumInterval, effectiveMaximumInterval);
        await EnsureLoadedAsync(cancellationToken);
        StarshipExpeditionSnapshot startedSnapshot;
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot is { Status: StarshipExpeditionStatus.Boarding or StarshipExpeditionStatus.Cruising or StarshipExpeditionStatus.EventOpen })
                throw new InvalidOperationException("A Starship Expedition is already active.");

            var previousSnapshot = _snapshot == null ? null : Clone(_snapshot);
            var now = _timeProvider.GetUtcNow();
            var expeditionSeed = seed ?? RandomNumberGenerator.GetInt32(int.MaxValue);
            _snapshot = new StarshipExpeditionSnapshot
            {
                ExpeditionId = Guid.NewGuid(),
                Seed = expeditionSeed,
                StartedBy = startedBy?.Trim() ?? string.Empty,
                Status = StarshipExpeditionStatus.Boarding,
                StartedAtUtc = now,
                UpdatedAtUtc = now,
                CurrentLeg = 0,
                LegCount = effectiveLegCount,
                MinimumEventIntervalSeconds = checked(effectiveMinimumInterval * 60),
                MaximumEventIntervalSeconds = checked(effectiveMaximumInterval * 60),
                StationIntervalLegs = settings.StationIntervalLegs,
                EventVoteDurationSeconds = settings.EventVoteDurationSeconds,
                BoardingDurationSeconds = settings.BoardingDurationSeconds,
                BoardingClosesAtUtc = now.AddSeconds(settings.BoardingDurationSeconds),
                ActiveCrewRewardsOnly = settings.ActiveCrewRewardsOnly,
                AnnounceSessionStatusInChat = settings.AnnounceSessionStatusInChat,
                SessionStartChatMessageTemplate = settings.SessionStartChatMessageTemplate,
                SessionEndChatMessageTemplate = settings.SessionEndChatMessageTemplate,
                SectorPool = settings.GetEffectiveSectors(),
                CurrentSector = settings.GetEffectiveSectors()[GetDeterministicIndex(expeditionSeed, 0, "initial-sector", settings.GetEffectiveSectors().Count)],
                CurrentRoute = "Uncharted"
            };

            await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            startedSnapshot = Clone(_snapshot);
        }
        finally
        {
            _stateLock.Release();
        }

        await AnnounceBoardingAsync(startedSnapshot);
        await FireTriggerAsync(DefaultCommandEventTypes.StarshipBoardingOpened, startedSnapshot, new Dictionary<string, string>
        {
            ["TriggeredBy"] = startedSnapshot.StartedBy,
            ["Duration"] = startedSnapshot.BoardingDurationSeconds.ToString(CultureInfo.InvariantCulture)
        });
        return startedSnapshot;
    }

    public async Task<StarshipExpeditionSnapshot?> StopExpeditionAsync(string? stoppedBy, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        StarshipExpeditionSnapshot? stoppedSnapshot = null;
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot is not { Status: StarshipExpeditionStatus.Boarding or StarshipExpeditionStatus.Cruising or StarshipExpeditionStatus.EventOpen }) return null;

            var previousSnapshot = Clone(_snapshot);
            _snapshot.Status = StarshipExpeditionStatus.Aborted;
            _snapshot.CurrentEvent = null;
            _snapshot.NextEventAtUtc = null;
            _snapshot.BoardingClosesAtUtc = null;
            await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            logger.LogInformation("Starship Expedition {ExpeditionId} stopped by {User}", _snapshot.ExpeditionId, stoppedBy ?? "System");
            stoppedSnapshot = Clone(_snapshot);
        }
        finally
        {
            _stateLock.Release();
        }

        if (stoppedSnapshot != null)
        {
            await AnnounceVoyageEndedAsync(stoppedSnapshot);
            await FireVoyageEndedTriggerAsync(stoppedSnapshot, stoppedBy);
        }
        return stoppedSnapshot;
    }

    public async Task<StarshipExpeditionSnapshot> AdvanceEventAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        StarshipExpeditionSnapshot previousSnapshot;
        StarshipExpeditionSnapshot advancedSnapshot;
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = GetActiveSnapshot();
            previousSnapshot = Clone(snapshot);
            var now = _timeProvider.GetUtcNow();
            if (snapshot.Status == StarshipExpeditionStatus.Boarding)
            {
                if (snapshot.Crew.Count == 0)
                    throw new InvalidOperationException("No crew has joined yet. Viewers can join with !starship join before launch.");
                snapshot.BoardingClosesAtUtc = null;
                OpenNextEventLocked(snapshot, now);
            }
            else if (snapshot.CurrentEvent != null)
                ResolveCurrentEventLocked(snapshot, now);
            else
                OpenNextEventLocked(snapshot, now);

            await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            advancedSnapshot = Clone(snapshot);
        }
        finally
        {
            _stateLock.Release();
        }

        await NotifyTransitionAsync(previousSnapshot, advancedSnapshot);
        return advancedSnapshot;
    }

    public async Task<StarshipExpeditionSnapshot> JoinCrewAsync(CommandEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var userId = RequireUserId(eventArgs);
        await EnsureLoadedAsync(cancellationToken);
        StarshipExpeditionSnapshot joinedSnapshot;
        bool added;
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = GetActiveSnapshot();
            if (snapshot.Status != StarshipExpeditionStatus.Boarding || snapshot.BoardingClosesAtUtc <= _timeProvider.GetUtcNow())
                throw new InvalidOperationException("The crew boarding window is closed. Join during the next expedition's boarding call.");
            var previousSnapshot = Clone(snapshot);
            added = AddCrewMember(snapshot, userId, eventArgs.Name, eventArgs.DisplayName, StarshipCrewEnrollmentMethod.ExplicitJoin);
            if (added)
                await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            joinedSnapshot = Clone(snapshot);
        }
        finally
        {
            _stateLock.Release();
        }

        if (added)
        {
            await FireTriggerAsync(DefaultCommandEventTypes.StarshipCrewJoined, joinedSnapshot, new Dictionary<string, string>
            {
                ["User"] = eventArgs.DisplayName,
                ["CrewCount"] = joinedSnapshot.Crew.Count.ToString(CultureInfo.InvariantCulture)
            }, eventArgs);
        }
        return joinedSnapshot;
    }

    public async Task<StarshipExpeditionSnapshot> VoteAsync(CommandEventArgs eventArgs, string choiceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        if (string.IsNullOrWhiteSpace(choiceId)) throw new ArgumentException("Choose one of the current event options.", nameof(choiceId));
        var userId = RequireUserId(eventArgs);
        await EnsureLoadedAsync(cancellationToken);
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = GetActiveSnapshot();
            if (snapshot.CurrentEvent == null || snapshot.Status != StarshipExpeditionStatus.EventOpen)
                throw new InvalidOperationException("There is no open Starship event to vote on.");
            if (!snapshot.Crew.Any(member => string.Equals(member.UserId, userId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Only crew members can vote. Join during the boarding window with !starship join.");

            var choice = snapshot.CurrentEvent.Choices.FirstOrDefault(option =>
                string.Equals(option.ChoiceId, choiceId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (choice == null)
                throw new ArgumentException("That is not one of the current event options.", nameof(choiceId));

            var previousSnapshot = Clone(snapshot);
            if (snapshot.CurrentEvent.Votes.Any(vote => string.Equals(vote.UserId, userId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Your choice for this event has already been recorded.");

            snapshot.CurrentEvent.Votes.Add(new StarshipEventVoteSnapshot { UserId = userId, ChoiceId = choice.ChoiceId });
            await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            return Clone(snapshot);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task AnnounceBoardingAsync(StarshipExpeditionSnapshot snapshot)
    {
        var defaultMessage = $"Starship Expedition boarding is open for {snapshot.BoardingDurationSeconds}s. Join before takeoff with !starship join.";
        var message = snapshot.AnnounceSessionStatusInChat
            ? snapshot.SessionStartChatMessageTemplate.Replace("{duration}", snapshot.BoardingDurationSeconds.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            : defaultMessage;
        if (string.IsNullOrWhiteSpace(message)) message = defaultMessage;
        if (!message.Contains("!starship join", StringComparison.OrdinalIgnoreCase))
            message = $"{message.Trim()} Join before takeoff with !starship join.";
        await SendChatAnnouncementAsync(message);
    }

    private async Task NotifyTransitionAsync(StarshipExpeditionSnapshot previous, StarshipExpeditionSnapshot current)
    {
        if (previous.CurrentEvent == null && current.CurrentEvent is { } openedEvent)
        {
            await AnnounceEventOpenedAsync(current, openedEvent);
            await FireTriggerAsync(DefaultCommandEventTypes.StarshipEventOpened, current, new Dictionary<string, string>
            {
                ["EventTitle"] = openedEvent.Title,
                ["EventType"] = openedEvent.EventType,
                ["Choices"] = string.Join(" | ", openedEvent.Choices.Select(choice => $"{choice.ChoiceId}: {choice.Label}")),
                ["Duration"] = current.EventVoteDurationSeconds.ToString(CultureInfo.InvariantCulture)
            });
        }

        if (current.CompletedEvents.Count > previous.CompletedEvents.Count)
        {
            var resolvedEvent = current.CompletedEvents[^1];
            var selectedChoice = resolvedEvent.Choices.FirstOrDefault(choice =>
                string.Equals(choice.ChoiceId, resolvedEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase));
            await SendChatAnnouncementAsync($"Starship event resolved: {resolvedEvent.Title}. Crew chose {selectedChoice?.Label ?? resolvedEvent.ResolvedChoiceId ?? "the default"}.");
            await FireTriggerAsync(DefaultCommandEventTypes.StarshipEventResolved, current, new Dictionary<string, string>
            {
                ["EventTitle"] = resolvedEvent.Title,
                ["EventType"] = resolvedEvent.EventType,
                ["Choice"] = selectedChoice?.Label ?? resolvedEvent.ResolvedChoiceId ?? string.Empty,
                ["VoteCount"] = resolvedEvent.Votes.Count.ToString(CultureInfo.InvariantCulture),
                ["Leg"] = resolvedEvent.Sequence.ToString(CultureInfo.InvariantCulture),
                ["Sector"] = resolvedEvent.Sector
            });
        }

        if (IsTerminal(current.Status) && !IsTerminal(previous.Status))
        {
            await AnnounceVoyageEndedAsync(current);
            await FireVoyageEndedTriggerAsync(current, current.Status == StarshipExpeditionStatus.Aborted ? current.StartedBy : null);
        }
    }

    private Task AnnounceEventOpenedAsync(StarshipExpeditionSnapshot snapshot, StarshipExpeditionEventSnapshot currentEvent)
    {
        var choices = string.Join(" | ", currentEvent.Choices.Select(choice => $"{choice.ChoiceId}: {choice.Label}"));
        var message = $"Starship vote: {currentEvent.Title} at {currentEvent.Sector}. {choices}. Crew vote with !starship vote <choice> within {snapshot.EventVoteDurationSeconds}s.";
        return SendChatAnnouncementAsync(message);
    }

    private async Task AnnounceVoyageEndedAsync(StarshipExpeditionSnapshot snapshot)
    {
        if (!snapshot.AnnounceSessionStatusInChat) return;
        var duration = Math.Max(0, (long)(_timeProvider.GetUtcNow() - snapshot.StartedAtUtc).TotalSeconds);
        var result = snapshot.Status == StarshipExpeditionStatus.Returned ? "returned" : "was aborted";
        var message = snapshot.SessionEndChatMessageTemplate
            .Replace("{count}", snapshot.Crew.Count.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{duration}", duration.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{result}", result, StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(message))
            message = $"Starship Expedition {result} with {snapshot.Crew.Count} crew after {duration}s.";
        await SendChatAnnouncementAsync(message);
    }

    private Task FireVoyageEndedTriggerAsync(StarshipExpeditionSnapshot snapshot, string? triggeredBy) =>
        FireTriggerAsync(DefaultCommandEventTypes.StarshipVoyageEnded, snapshot, new Dictionary<string, string>
        {
            ["Result"] = snapshot.Status.ToString(),
            ["Duration"] = Math.Max(0, (long)(_timeProvider.GetUtcNow() - snapshot.StartedAtUtc).TotalSeconds).ToString(CultureInfo.InvariantCulture),
            ["TriggeredBy"] = triggeredBy ?? snapshot.StartedBy
        });

    private async Task FireTriggerAsync(
        string eventType,
        StarshipExpeditionSnapshot snapshot,
        Dictionary<string, string> additionalVariables,
        CommandEventArgs? eventArgs = null)
    {
        if (defaultCommandTriggerService == null) return;
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Game"] = "Starship Expedition",
            ["ExpeditionId"] = snapshot.ExpeditionId.ToString(),
            ["Leg"] = Math.Min(snapshot.CurrentLeg + 1, snapshot.LegCount).ToString(CultureInfo.InvariantCulture),
            ["LegCount"] = snapshot.LegCount.ToString(CultureInfo.InvariantCulture),
            ["Sector"] = snapshot.CurrentSector,
            ["CrewCount"] = snapshot.Crew.Count.ToString(CultureInfo.InvariantCulture),
            ["StartedBy"] = snapshot.StartedBy
        };
        foreach (var (key, value) in additionalVariables)
            variables[key] = value;
        eventArgs ??= new CommandEventArgs
        {
            Command = DefaultCommandName,
            Name = snapshot.StartedBy,
            DisplayName = snapshot.StartedBy,
            UserId = snapshot.StartedBy
        };

        try
        {
            await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(DefaultCommandName, eventType, eventArgs, variables);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed firing Starship Expedition trigger {EventType}", eventType);
        }
    }

    private async Task SendChatAnnouncementAsync(string message)
    {
        try
        {
            await ServiceBackbone.SendChatMessage(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed sending Starship Expedition chat announcement");
        }
    }

    private static bool IsTerminal(StarshipExpeditionStatus status) =>
        status is StarshipExpeditionStatus.Returned or StarshipExpeditionStatus.Aborted;

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_hasLoaded) return;
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (_hasLoaded) return;
            _snapshot = await stateStore.LoadAsync(cancellationToken);
            _hasLoaded = true;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task RunSchedulerAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await ProcessDueTransitionAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed processing due Starship Expedition event");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Starship Expedition scheduler stopped unexpectedly");
        }
    }

    internal async Task ProcessDueTransitionAsync(CancellationToken cancellationToken = default)
    {
        StarshipExpeditionSnapshot? previousSnapshot = null;
        StarshipExpeditionSnapshot? updatedSnapshot = null;
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = _snapshot;
            if (snapshot == null || snapshot.Status is StarshipExpeditionStatus.Returned or StarshipExpeditionStatus.Aborted) return;

            var now = _timeProvider.GetUtcNow();
            if (snapshot.Status == StarshipExpeditionStatus.Boarding && snapshot.BoardingClosesAtUtc <= now)
            {
                previousSnapshot = Clone(snapshot);
                snapshot.BoardingClosesAtUtc = null;
                if (snapshot.Crew.Count == 0)
                {
                    snapshot.Status = StarshipExpeditionStatus.Aborted;
                    snapshot.NextEventAtUtc = null;
                }
                else
                {
                    OpenNextEventLocked(snapshot, now);
                }
                await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
                updatedSnapshot = Clone(snapshot);
            }
            else if (snapshot.CurrentEvent is { } currentEvent && currentEvent.ClosesAtUtc <= now)
            {
                previousSnapshot = Clone(snapshot);
                ResolveCurrentEventLocked(snapshot, now);
                await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
                updatedSnapshot = Clone(snapshot);
            }
            else if (snapshot.Status == StarshipExpeditionStatus.Cruising && snapshot.NextEventAtUtc <= now)
            {
                previousSnapshot = Clone(snapshot);
                OpenNextEventLocked(snapshot, now);
                await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
                updatedSnapshot = Clone(snapshot);
            }
        }
        finally
        {
            _stateLock.Release();
        }

        if (previousSnapshot != null && updatedSnapshot != null)
            await NotifyTransitionAsync(previousSnapshot, updatedSnapshot);
    }

    private void OpenNextEventLocked(StarshipExpeditionSnapshot snapshot, DateTimeOffset now)
    {
        snapshot.BoardingClosesAtUtc = null;
        if (snapshot.CurrentLeg >= snapshot.LegCount)
        {
            snapshot.Status = StarshipExpeditionStatus.Returned;
            snapshot.NextEventAtUtc = null;
            snapshot.CurrentEvent = null;
            return;
        }

        var sequence = snapshot.NextEventSequence;
        var definition = GetEventDefinition(snapshot, sequence);
        snapshot.CurrentEvent = new StarshipExpeditionEventSnapshot
        {
            EventId = CreateDeterministicEventId(snapshot.ExpeditionId, sequence),
            Sequence = sequence,
            EventType = definition.EventType,
            Title = definition.Title,
            Description = definition.Description,
            Sector = snapshot.CurrentSector,
            OpenedAtUtc = now,
            ClosesAtUtc = now.AddSeconds(snapshot.EventVoteDurationSeconds),
            Choices = definition.Choices,
            DefaultChoiceId = definition.DefaultChoiceId,
            Votes = []
        };
        snapshot.NextEventSequence++;
        snapshot.Status = StarshipExpeditionStatus.EventOpen;
        snapshot.NextEventAtUtc = null;
    }

    private void ResolveCurrentEventLocked(StarshipExpeditionSnapshot snapshot, DateTimeOffset now)
    {
        var currentEvent = snapshot.CurrentEvent ?? throw new InvalidOperationException("There is no open event to resolve.");
        var voteCounts = currentEvent.Votes
            .GroupBy(vote => vote.ChoiceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { ChoiceId = group.Key, Count = group.Count() })
            .ToList();
        var maxVotes = voteCounts.Count == 0 ? 0 : voteCounts.Max(vote => vote.Count);
        var leaders = voteCounts.Where(vote => vote.Count == maxVotes).Select(vote => vote.ChoiceId).ToList();
        currentEvent.ResolvedChoiceId = leaders.Count == 1
            ? leaders[0]
            : currentEvent.DefaultChoiceId;
        currentEvent.ResolvedAtUtc = now;
        snapshot.CompletedEvents.Add(Clone(currentEvent));

        if (currentEvent.EventType == "RouteChoice")
            snapshot.CurrentRoute = currentEvent.ResolvedChoiceId == "detour" ? "Survey Route" : "Direct Route";

        if (currentEvent.EventType != "StationStop")
            snapshot.CurrentSector = GetNextSector(snapshot, currentEvent.Sequence);

        snapshot.CurrentLeg++;
        snapshot.CurrentEvent = null;
        if (snapshot.CurrentLeg >= snapshot.LegCount)
        {
            snapshot.Status = StarshipExpeditionStatus.Returned;
            snapshot.NextEventAtUtc = null;
            return;
        }

        snapshot.Status = StarshipExpeditionStatus.Cruising;
        var interval = GetDeterministicIntervalSeconds(snapshot);
        snapshot.NextEventAtUtc = now.AddSeconds(interval);
    }

    private async Task PersistAndBroadcastLockedAsync(CancellationToken cancellationToken)
    {
        if (_snapshot == null) return;
        _snapshot.UpdatedAtUtc = _timeProvider.GetUtcNow();
        await stateStore.SaveAsync(_snapshot, cancellationToken);
        var payload = JsonSerializer.Serialize(new
        {
            type = "expedition_state",
            snapshot = StarshipExpeditionOverlaySnapshot.From(_snapshot)
        }, JsonOptions);
        try
        {
            await webSocketMessenger.AddToQueue(WsTopics.Starship, payload);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed broadcasting Starship Expedition state {ExpeditionId}", _snapshot.ExpeditionId);
        }
    }

    private async Task PersistMutationWithRollbackAsync(StarshipExpeditionSnapshot? previousSnapshot, CancellationToken cancellationToken)
    {
        try
        {
            await PersistAndBroadcastLockedAsync(cancellationToken);
        }
        catch
        {
            _snapshot = previousSnapshot;
            throw;
        }
    }

    private StarshipExpeditionSnapshot GetActiveSnapshot()
    {
        if (_snapshot is not { Status: StarshipExpeditionStatus.Boarding or StarshipExpeditionStatus.Cruising or StarshipExpeditionStatus.EventOpen })
            throw new InvalidOperationException("There is no active Starship Expedition.");
        return _snapshot;
    }

    private static EventDefinition GetEventDefinition(StarshipExpeditionSnapshot snapshot, int sequence)
    {
        if (sequence == 1)
        {
            return new EventDefinition("RouteChoice", "Choose a Route", "The crew can take a direct route or detour through a survey area.",
                [new() { ChoiceId = "direct", Label = "Take the direct route" }, new() { ChoiceId = "detour", Label = "Take the survey detour" }], "direct");
        }

        if (snapshot.CurrentLeg > 0 && snapshot.StationIntervalLegs > 0 && snapshot.CurrentLeg % snapshot.StationIntervalLegs == 0)
        {
            return new EventDefinition("StationStop", "Space Station", "A station offers a brief resupply and banking stop.",
                [new() { ChoiceId = "resupply", Label = "Resupply" }, new() { ChoiceId = "repair", Label = "Repair the ship" }, new() { ChoiceId = "continue", Label = "Continue the voyage" }], "resupply");
        }

        var routeEvents = snapshot.CurrentRoute switch
        {
            "Direct Route" => RandomEvents.Where(definition => definition.EventType != "SalvageSite").ToArray(),
            "Survey Route" => RandomEvents.Where(definition => definition.EventType != "Hazard").ToArray(),
            _ => RandomEvents
        };
        return routeEvents[GetDeterministicIndex(snapshot.Seed, sequence, $"event-type:{snapshot.CurrentRoute}", routeEvents.Length)];
    }

    private static string GetNextSector(StarshipExpeditionSnapshot snapshot, int eventSequence)
    {
        var currentSectorIndex = Array.FindIndex(snapshot.SectorPool.ToArray(), sector =>
            string.Equals(sector, snapshot.CurrentSector, StringComparison.OrdinalIgnoreCase));
        if (currentSectorIndex < 0) currentSectorIndex = 0;
        var sectorCount = snapshot.SectorPool.Count;
        if (sectorCount < 2) throw new InvalidOperationException("The expedition sector pool requires at least two sectors.");
        var offset = 1 + GetDeterministicIndex(snapshot.Seed, eventSequence, $"sector:{snapshot.CurrentRoute}", sectorCount - 1);
        return snapshot.SectorPool[(currentSectorIndex + offset) % sectorCount];
    }

    private static int GetDeterministicIntervalSeconds(StarshipExpeditionSnapshot snapshot)
    {
        var intervalRange = snapshot.MaximumEventIntervalSeconds - snapshot.MinimumEventIntervalSeconds + 1;
        return snapshot.MinimumEventIntervalSeconds + GetDeterministicIndex(snapshot.Seed, snapshot.NextEventSequence, "event-interval", intervalRange);
    }

    private static int GetDeterministicIndex(int seed, int sequence, string purpose, int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        var input = Encoding.UTF8.GetBytes($"{seed}:{sequence}:{purpose}");
        var hash = SHA256.HashData(input);
        return (int)(BinaryPrimitives.ReadUInt32LittleEndian(hash) % (uint)count);
    }

    private static Guid CreateDeterministicEventId(Guid expeditionId, int sequence)
    {
        var input = Encoding.UTF8.GetBytes($"starship:{expeditionId:N}:{sequence}");
        var hash = SHA256.HashData(input);
        return new Guid(hash.AsSpan(0, 16));
    }

    private static Guid CreateDeterministicPreviewId(int seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"starship-preview:{seed}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private bool AddCrewMember(
        StarshipExpeditionSnapshot snapshot,
        string userId,
        string? userName,
        string? displayName,
        StarshipCrewEnrollmentMethod enrollmentMethod)
    {
        if (snapshot.Crew.Any(member => string.Equals(member.UserId, userId, StringComparison.OrdinalIgnoreCase))) return false;
        snapshot.Crew.Add(new StarshipCrewMemberSnapshot
        {
            UserId = userId,
            UserName = string.IsNullOrWhiteSpace(userName) ? displayName ?? userId : userName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? userName ?? userId : displayName,
            JoinedAtUtc = _timeProvider.GetUtcNow(),
            EnrollmentMethod = enrollmentMethod
        });
        return true;
    }

    private static string RequireUserId(CommandEventArgs eventArgs)
    {
        if (!string.IsNullOrWhiteSpace(eventArgs.UserId)) return eventArgs.UserId;
        throw new InvalidOperationException("Could not identify your Twitch account for the expedition.");
    }

    private static StarshipExpeditionSnapshot Clone(StarshipExpeditionSnapshot snapshot) =>
        JsonSerializer.Deserialize<StarshipExpeditionSnapshot>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)
        ?? throw new InvalidOperationException("Could not clone Starship expedition state.");

    private static StarshipExpeditionEventSnapshot Clone(StarshipExpeditionEventSnapshot expeditionEvent) =>
        JsonSerializer.Deserialize<StarshipExpeditionEventSnapshot>(JsonSerializer.Serialize(expeditionEvent, JsonOptions), JsonOptions)
        ?? throw new InvalidOperationException("Could not clone Starship event state.");

    private static void ValidateStartOptions(int legCount, int minimumEventIntervalMinutes, int maximumEventIntervalMinutes)
    {
        if (legCount is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(legCount), "Leg count must be between 1 and 24.");
        if (minimumEventIntervalMinutes is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(minimumEventIntervalMinutes), "Minimum event interval must be between 1 and 120 minutes.");
        if (maximumEventIntervalMinutes < minimumEventIntervalMinutes || maximumEventIntervalMinutes > 120)
            throw new ArgumentOutOfRangeException(nameof(maximumEventIntervalMinutes), "Maximum event interval must be at least the minimum and no more than 120 minutes.");
    }

    private static int? ParseOptionalInteger(IReadOnlyList<string> args, int index) =>
        index < args.Count && int.TryParse(args[index], out var parsed) ? parsed : null;

    private static string FormatStatus(StarshipExpeditionSnapshot? snapshot) => snapshot == null
        ? "No Starship Expedition is active."
        : $"Starship Expedition: {snapshot.Status}, leg {snapshot.CurrentLeg}/{snapshot.LegCount}, sector {snapshot.CurrentSector}.";

    private sealed record EventDefinition(
        string EventType,
        string Title,
        string Description,
        List<StarshipEventChoiceSnapshot> Choices,
        string DefaultChoiceId);
}