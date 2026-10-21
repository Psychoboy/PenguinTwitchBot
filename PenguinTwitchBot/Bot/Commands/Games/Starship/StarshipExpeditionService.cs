using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Application.Notifications;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed class StarshipExpeditionService(
    IServiceBackbone serviceBackbone,
    ICommandHandler commandHandler,
    IStarshipExpeditionStateStore stateStore,
    IWebSocketMessenger webSocketMessenger,
    ILogger<StarshipExpeditionService> logger,
    IPenguinDispatcher dispatcher,
    TimeProvider? timeProvider = null)
    : BaseCommandService(serviceBackbone, commandHandler, "StarshipExpedition", dispatcher), IHostedService, IStarshipExpeditionService
{
    public const string DefaultCommandName = "starship";
    public const int DefaultLegCount = 5;
    public const int DefaultMinimumEventIntervalMinutes = 15;
    public const int DefaultMaximumEventIntervalMinutes = 30;
    public const int DefaultStationIntervalLegs = 2;
    public const int DefaultVoteDurationSeconds = 60;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] Sectors =
    [
        "Kepler Reach",
        "Ternion Belt",
        "Vela Expanse",
        "Asterion Drift",
        "Pioneer Verge"
    ];

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

                    var legCount = ParseInteger(args, 1, DefaultLegCount);
                    var minimumInterval = ParseInteger(args, 2, DefaultMinimumEventIntervalMinutes);
                    var maximumInterval = ParseInteger(args, 3, DefaultMaximumEventIntervalMinutes);
                    var started = await StartExpeditionAsync(eventArgs.DisplayName, legCount, minimumInterval, maximumInterval);
                    await RespondWithMessage(eventArgs, $"Starship Expedition started: {started.LegCount} legs, seed {started.Seed}.");
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

    public async Task<StarshipExpeditionSnapshot> StartExpeditionAsync(
        string? startedBy,
        int legCount = DefaultLegCount,
        int minimumEventIntervalMinutes = DefaultMinimumEventIntervalMinutes,
        int maximumEventIntervalMinutes = DefaultMaximumEventIntervalMinutes,
        int? seed = null,
        CancellationToken cancellationToken = default)
    {
        ValidateStartOptions(legCount, minimumEventIntervalMinutes, maximumEventIntervalMinutes);
        await EnsureLoadedAsync(cancellationToken);
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot is { Status: StarshipExpeditionStatus.Cruising or StarshipExpeditionStatus.EventOpen })
                throw new InvalidOperationException("A Starship Expedition is already active.");

            var previousSnapshot = _snapshot == null ? null : Clone(_snapshot);
            var now = _timeProvider.GetUtcNow();
            var expeditionSeed = seed ?? RandomNumberGenerator.GetInt32(int.MaxValue);
            _snapshot = new StarshipExpeditionSnapshot
            {
                ExpeditionId = Guid.NewGuid(),
                Seed = expeditionSeed,
                StartedBy = startedBy?.Trim() ?? string.Empty,
                Status = StarshipExpeditionStatus.Cruising,
                StartedAtUtc = now,
                UpdatedAtUtc = now,
                CurrentLeg = 0,
                LegCount = legCount,
                MinimumEventIntervalSeconds = checked(minimumEventIntervalMinutes * 60),
                MaximumEventIntervalSeconds = checked(maximumEventIntervalMinutes * 60),
                StationIntervalLegs = DefaultStationIntervalLegs,
                EventVoteDurationSeconds = DefaultVoteDurationSeconds,
                CurrentSector = Sectors[GetDeterministicIndex(expeditionSeed, 0, "initial-sector", Sectors.Length)],
                CurrentRoute = "Uncharted"
            };

            OpenNextEventLocked(_snapshot, now);
            await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            return Clone(_snapshot);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<StarshipExpeditionSnapshot?> StopExpeditionAsync(string? stoppedBy, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot is not { Status: StarshipExpeditionStatus.Cruising or StarshipExpeditionStatus.EventOpen }) return null;

            var previousSnapshot = Clone(_snapshot);
            _snapshot.Status = StarshipExpeditionStatus.Aborted;
            _snapshot.CurrentEvent = null;
            _snapshot.NextEventAtUtc = null;
            await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            logger.LogInformation("Starship Expedition {ExpeditionId} stopped by {User}", _snapshot.ExpeditionId, stoppedBy ?? "System");
            return Clone(_snapshot);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<StarshipExpeditionSnapshot> AdvanceEventAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = GetActiveSnapshot();
            var previousSnapshot = Clone(snapshot);
            var now = _timeProvider.GetUtcNow();
            if (snapshot.CurrentEvent != null)
                ResolveCurrentEventLocked(snapshot, now);
            else
                OpenNextEventLocked(snapshot, now);

            await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            return Clone(snapshot);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<StarshipExpeditionSnapshot> JoinCrewAsync(CommandEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var userId = RequireUserId(eventArgs);
        await EnsureLoadedAsync(cancellationToken);
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = GetActiveSnapshot();
            var previousSnapshot = Clone(snapshot);
            AddCrewMember(snapshot, userId, eventArgs.Name, eventArgs.DisplayName, StarshipCrewEnrollmentMethod.ExplicitJoin);
            await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            return Clone(snapshot);
        }
        finally
        {
            _stateLock.Release();
        }
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

            var choice = snapshot.CurrentEvent.Choices.FirstOrDefault(option =>
                string.Equals(option.ChoiceId, choiceId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (choice == null)
                throw new ArgumentException("That is not one of the current event options.", nameof(choiceId));

            var previousSnapshot = Clone(snapshot);
            AddCrewMember(snapshot, userId, eventArgs.Name, eventArgs.DisplayName, StarshipCrewEnrollmentMethod.AutomaticAction);
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
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = _snapshot;
            if (snapshot == null || snapshot.Status is StarshipExpeditionStatus.Returned or StarshipExpeditionStatus.Aborted) return;

            var now = _timeProvider.GetUtcNow();
            if (snapshot.CurrentEvent is { } currentEvent && currentEvent.ClosesAtUtc <= now)
            {
                var previousSnapshot = Clone(snapshot);
                ResolveCurrentEventLocked(snapshot, now);
                await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            }
            else if (snapshot.Status == StarshipExpeditionStatus.Cruising && snapshot.NextEventAtUtc <= now)
            {
                var previousSnapshot = Clone(snapshot);
                OpenNextEventLocked(snapshot, now);
                await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private void OpenNextEventLocked(StarshipExpeditionSnapshot snapshot, DateTimeOffset now)
    {
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
        var payload = JsonSerializer.Serialize(new { type = "expedition_state", snapshot = _snapshot }, JsonOptions);
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
        if (_snapshot is not { Status: StarshipExpeditionStatus.Cruising or StarshipExpeditionStatus.EventOpen })
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
        var currentSectorIndex = Array.IndexOf(Sectors, snapshot.CurrentSector);
        if (currentSectorIndex < 0) currentSectorIndex = 0;
        var offset = 1 + GetDeterministicIndex(snapshot.Seed, eventSequence, $"sector:{snapshot.CurrentRoute}", Sectors.Length - 1);
        return Sectors[(currentSectorIndex + offset) % Sectors.Length];
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

    private void AddCrewMember(
        StarshipExpeditionSnapshot snapshot,
        string userId,
        string? userName,
        string? displayName,
        StarshipCrewEnrollmentMethod enrollmentMethod)
    {
        if (snapshot.Crew.Any(member => string.Equals(member.UserId, userId, StringComparison.OrdinalIgnoreCase))) return;
        snapshot.Crew.Add(new StarshipCrewMemberSnapshot
        {
            UserId = userId,
            UserName = string.IsNullOrWhiteSpace(userName) ? displayName ?? userId : userName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? userName ?? userId : displayName,
            JoinedAtUtc = _timeProvider.GetUtcNow(),
            EnrollmentMethod = enrollmentMethod
        });
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

    private static int ParseInteger(IReadOnlyList<string> args, int index, int defaultValue) =>
        index < args.Count && int.TryParse(args[index], out var parsed) ? parsed : defaultValue;

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