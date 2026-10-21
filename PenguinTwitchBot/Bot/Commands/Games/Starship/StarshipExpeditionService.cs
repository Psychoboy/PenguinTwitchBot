using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
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
    IDefaultCommandTriggerService? defaultCommandTriggerService = null,
    IPointsSystem? pointsSystem = null,
    IViewerFeature? viewerFeature = null)
    : BaseCommandService(serviceBackbone, commandHandler, "StarshipExpedition", dispatcher), IHostedService, IStarshipExpeditionService
{
    public const string DefaultCommandName = "starship";
    public const string ShortVoteCommandName = "sv";
    public const string ShortJoinCommandName = "sj";
    public const string ShortConfirmCommandName = "sc";
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
        new("SignalDetected", "Unidentified Signal", "A signal from {subject} is repeating at the edge of sensor range. Scan it, trace its origin, then investigate - or move on.",
            [
                new() { ChoiceId = "scan", Label = "Scan the signal", ContinuesEvent = true, GrantsFlag = "scanned", RevealsRarity = true, Story = "{crew} decodes the looping message from {subject}: a plea in a long-dead dialect, guarding {loot}." },
                new() { ChoiceId = "trace", Label = "Trace its origin", ContinuesEvent = true, GrantsFlag = "traced", SuccessChanceModifierPercent = 15, RewardMultiplierPercent = 85, RiskMultiplierPercent = 60, Story = "{crewlist} triangulate {subject} to a hidden dock deep in the {sector}." },
                new() { ChoiceId = "investigate", Label = "Investigate the source", Story = "{crew} takes a shuttle out to {subject}." },
                new() { ChoiceId = "ignore", Label = "Continue on course", EndsWithoutReward = true, Story = "The crew lets {subject} fade into static." }
            ], "ignore",
            "It was real: the crew recovers {loot}.",
            "It was a decoy. The shuttle limps home empty and the crew loses cargo."),
        new("SalvageSite", "Derelict Salvage", "A damaged vessel, {subject}, is drifting near the route. Scan it, secure the area, then recover the salvage - or pass.",
            [
                new() { ChoiceId = "scan", Label = "Scan the wreck", ContinuesEvent = true, GrantsFlag = "scanned", RevealsRarity = true, Story = "{crew} sweeps the sensors over {subject}. Warm signatures flicker behind the cargo hold - it looks like {loot}." },
                new() { ChoiceId = "secure", Label = "Secure the area", ContinuesEvent = true, GrantsFlag = "secured", SuccessChanceModifierPercent = 20, RewardMultiplierPercent = 80, RiskMultiplierPercent = 50, Story = "{crewlist} lock docking clamps onto {subject} and seal its breached corridors." },
                new() { ChoiceId = "salvage", Label = "Recover salvage", Story = "{crew} boards {subject} and starts cutting toward the cargo hold." },
                new() { ChoiceId = "pass", Label = "Pass it by", EndsWithoutReward = true, Story = "The crew leaves {subject} to drift on." }
            ], "pass",
            "The hatch gives way and the crew hauls out {loot}.",
            "A reactor surge rips through {subject}. The crew drops {loot} and flees."),
        new("Hazard", "Navigation Hazard", "{Subject} has appeared across the current flight path.",
            [
                new() { ChoiceId = "maneuver", Label = "Maneuver around it", Story = "{crew} takes the helm and arcs around {subject}." },
                new() { ChoiceId = "shield", Label = "Raise shields", SuccessChanceModifierPercent = 20, RewardMultiplierPercent = 80, Story = "{crew} diverts power to the shields as {subject} closes in." },
                new() { ChoiceId = "hold", Label = "Hold course", SuccessChanceModifierPercent = -15, RewardMultiplierPercent = 130, Story = "{crew} grits their teeth and holds course through {subject}." }
            ], "maneuver",
            "The crew clears {subject} and scoops up {loot}.",
            "{Subject} clips the hull and cargo is lost.")
    ];

    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly SemaphoreSlim _payoutLock = new(1, 1);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private StarshipExpeditionSnapshot? _snapshot;
    private StarshipAppearanceSettings? _appearance;
    private bool _hasLoaded;
    private CancellationTokenSource? _schedulerCancellation;
    private Task? _schedulerTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Register();
        await EnsureLoadedAsync(cancellationToken);
        await ProcessPendingBankedPayoutsAsync(cancellationToken);
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
                description: "Join or vote in the shared Starship Expedition; confirm disclosed risky choices. Moderators can start, stop, and advance it.");
        await RegisterDefaultCommand(ShortVoteCommandName, this, ModuleName, Rank.Viewer, userCooldown: 2,
            description: "Shortcut for !starship vote <option number>.");
        await RegisterDefaultCommand(ShortJoinCommandName, this, ModuleName, Rank.Viewer, userCooldown: 2,
            description: "Shortcut for !starship join.");
        await RegisterDefaultCommand(ShortConfirmCommandName, this, ModuleName, Rank.Viewer, userCooldown: 2,
            description: "Shortcut for !starship confirm.");
    }

    public override async Task OnCommand(object? sender, CommandEventArgs eventArgs)
    {
        var args = eventArgs.Args.Count > 0
            ? eventArgs.Args.ToList()
            : (eventArgs.Arg ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var shortSubCommand = eventArgs.Command?.Trim().TrimStart('!').ToLowerInvariant() switch
        {
            ShortVoteCommandName => "vote",
            ShortJoinCommandName => "join",
            ShortConfirmCommandName => "confirm",
            _ => null
        };
        if (shortSubCommand != null) args.Insert(0, shortSubCommand);
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
                    var joined = await JoinCrewAsync(eventArgs);
                    await RespondWithMessage(eventArgs, $"You joined the Starship Expedition crew ({joined.Crew.Count} aboard).{DescribePendingShare(joined, eventArgs.UserId)}");
                    return;

                case "vote":
                    if (args.Count < 2)
                    {
                        await RespondWithMessage(eventArgs, "Choose one of the current Starship event options: !sv <number>");
                        return;
                    }

                    var (voted, enrolledByVote) = await VoteCoreAsync(eventArgs, args[1], CancellationToken.None);
                    var enrollNote = enrolledByVote
                        ? $" You joined the crew ({voted.Crew.Count} aboard).{DescribePendingShare(voted, eventArgs.UserId)}"
                        : string.Empty;
                    var riskConfirmation = voted.CurrentEvent?.PendingRiskConfirmations.FirstOrDefault(item =>
                        string.Equals(item.UserId, eventArgs.UserId, StringComparison.OrdinalIgnoreCase));
                    if (riskConfirmation != null)
                    {
                        var rarityText = string.IsNullOrEmpty(riskConfirmation.RevealedRarity)
                            ? "Rarity: unknown (scan to reveal it first)."
                            : $"Rarity: {riskConfirmation.RevealedRarity}.";
                        await RespondWithMessage(eventArgs,
                            $"Risk check: your unbanked {riskConfirmation.PointTypeName} balance is {riskConfirmation.ExposedBalance}; failing can lose up to {riskConfirmation.MaximumLoss}. " +
                            $"Success chance {riskConfirmation.SuccessChancePercent}%, rewards x{StarshipExpeditionNarrative.FormatMultiplier(riskConfirmation.RewardMultiplierPercent)}. {rarityText} Confirm with !sc within 30 seconds.");
                    }
                    else
                    {
                        var votedChoice = voted.CurrentEvent?.Votes
                            .Where(vote => string.Equals(vote.UserId, eventArgs.UserId, StringComparison.OrdinalIgnoreCase))
                            .Select(vote => voted.CurrentEvent.Choices.FirstOrDefault(choice =>
                                string.Equals(choice.ChoiceId, vote.ChoiceId, StringComparison.OrdinalIgnoreCase)))
                            .FirstOrDefault();
                        var tally = voted.CurrentEvent?.Votes.Count ?? 0;
                        await RespondWithMessage(eventArgs,
                            $"Your Starship vote was recorded: {votedChoice?.Label ?? "your choice"} ({tally} vote{(tally == 1 ? string.Empty : "s")} so far).{enrollNote}");
                    }
                    return;

                case "confirm":
                    var confirmed = await ConfirmRiskActionAsync(eventArgs);
                    var confirmedChoice = confirmed.CurrentEvent?.Votes
                        .Where(vote => string.Equals(vote.UserId, eventArgs.UserId, StringComparison.OrdinalIgnoreCase))
                        .Select(vote => confirmed.CurrentEvent.Choices.FirstOrDefault(choice =>
                            string.Equals(choice.ChoiceId, vote.ChoiceId, StringComparison.OrdinalIgnoreCase)))
                        .FirstOrDefault();
                    await RespondWithMessage(eventArgs, $"Confirmed and committed: {confirmedChoice?.Label ?? "your risky action"}. Results are announced when the vote ends.");
                    return;

                case "status":
                    await RespondWithMessage(eventArgs, FormatStatus(await GetSnapshotAsync()));
                    return;

                default:
                        await RespondWithMessage(eventArgs, "Use !sj to join, !sv <number> to vote, or !sc to confirm a risk prompt (long forms: !starship join|vote|confirm). Moderators: !starship start [legs] [min minutes] [max minutes], !starship advance, or !starship stop.");
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
            RewardProfiles = JsonSerializer.Deserialize<List<StarshipRewardProfile>>(
                JsonSerializer.Serialize(previewSettings.RewardProfiles, JsonOptions), JsonOptions) ?? [],
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

    public async Task<StarshipExpeditionSnapshot> AdvanceEventAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var observedSnapshot = await GetSnapshotAsync(cancellationToken);
            var profile = observedSnapshot?.CurrentEvent == null
                ? null
                : GetRewardProfile(observedSnapshot, observedSnapshot.CurrentEvent.EventType);
            var eligibleUserIds = profile == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : await GetEligibleCrewUserIdsAsync(observedSnapshot!, cancellationToken);
            StarshipExpeditionSnapshot? previousSnapshot = null;
            StarshipExpeditionSnapshot? advancedSnapshot = null;
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                var snapshot = GetActiveSnapshot();
                if (observedSnapshot == null || !IsSameTransitionPoint(snapshot, observedSnapshot))
                    continue;

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
                    ResolveCurrentEventAndRewardsLocked(snapshot, now, eligibleUserIds);
                else
                    OpenNextEventLocked(snapshot, now);

                await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
                advancedSnapshot = Clone(snapshot);
            }
            finally
            {
                _stateLock.Release();
            }

            if (advancedSnapshot == null) continue;
            await NotifyTransitionAsync(previousSnapshot!, advancedSnapshot);
            await ProcessPendingBankedPayoutsAsync(cancellationToken);
            return await GetSnapshotAsync(cancellationToken) ?? advancedSnapshot;
        }

        throw new InvalidOperationException("The Starship state changed repeatedly while advancing. Please try again.");
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
        await ProcessPendingBankedPayoutsAsync(cancellationToken);
        var rewardProfiles = await SnapshotRewardProfilesAsync(settings.RewardProfiles, cancellationToken);
        StarshipExpeditionSnapshot startedSnapshot;
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot is { Status: StarshipExpeditionStatus.Boarding or StarshipExpeditionStatus.Cruising or StarshipExpeditionStatus.EventOpen })
                throw new InvalidOperationException("A Starship Expedition is already active.");
            if (_snapshot?.BankedPayouts.Any(payout => !payout.IsPaid) == true)
                throw new InvalidOperationException("The previous Starship Expedition still has an unbanked crew payout pending. Resolve the payout before starting another voyage.");

            var previousSnapshot = _snapshot == null ? null : Clone(_snapshot);
            var now = _timeProvider.GetUtcNow();
            var expeditionSeed = seed ?? RandomNumberGenerator.GetInt32(int.MaxValue);
            _snapshot = new StarshipExpeditionSnapshot
            {
                ExpeditionId = Guid.NewGuid(),
                Seed = expeditionSeed,
                StartedBy = startedBy?.Trim() ?? string.Empty,
                Ship = StarshipExpeditionStory.GenerateShip(expeditionSeed),
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
                RewardProfiles = rewardProfiles,
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
            QueuePayouts(_snapshot, _snapshot.ExpeditionId);
            AppendVoyageEndedActivity(_snapshot, _timeProvider.GetUtcNow());
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
            await ProcessPendingBankedPayoutsAsync(cancellationToken);
        }
        return stoppedSnapshot == null ? null : await GetSnapshotAsync(cancellationToken) ?? stoppedSnapshot;
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
            if (snapshot.Status == StarshipExpeditionStatus.Boarding && snapshot.BoardingClosesAtUtc <= _timeProvider.GetUtcNow())
                throw new InvalidOperationException("The crew boarding window just closed - the voyage is about to launch. Join once it is under way.");
            var previousSnapshot = Clone(snapshot);
            added = AddCrewMember(snapshot, userId, eventArgs.Name, eventArgs.DisplayName, StarshipCrewEnrollmentMethod.ExplicitJoin,
                GetEffectiveFromSequence(snapshot));
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

    public async Task<StarshipExpeditionSnapshot> VoteAsync(CommandEventArgs eventArgs, string choiceId, CancellationToken cancellationToken = default) =>
        (await VoteCoreAsync(eventArgs, choiceId, cancellationToken)).Snapshot;

    private async Task<(StarshipExpeditionSnapshot Snapshot, bool Enrolled)> VoteCoreAsync(CommandEventArgs eventArgs, string choiceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        if (string.IsNullOrWhiteSpace(choiceId)) throw new ArgumentException("Choose one of the current event options.", nameof(choiceId));
        var userId = RequireUserId(eventArgs);
        await EnsureLoadedAsync(cancellationToken);
        StarshipExpeditionSnapshot result;
        var enrolled = false;
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = GetActiveSnapshot();
            if (snapshot.CurrentEvent == null || snapshot.Status != StarshipExpeditionStatus.EventOpen)
                throw new InvalidOperationException("There is no open Starship event to vote on.");

            var trimmedChoice = choiceId.Trim();
            var choice = snapshot.CurrentEvent.Choices.FirstOrDefault(option =>
                string.Equals(option.ChoiceId, trimmedChoice, StringComparison.OrdinalIgnoreCase));
            if (choice == null && int.TryParse(trimmedChoice, NumberStyles.None, CultureInfo.InvariantCulture, out var choiceNumber) &&
                choiceNumber >= 1 && choiceNumber <= snapshot.CurrentEvent.Choices.Count)
                choice = snapshot.CurrentEvent.Choices[choiceNumber - 1];
            if (choice == null)
                throw new ArgumentException("That is not one of the current event options. Use the option number, e.g. !sv 1.", nameof(choiceId));

            var previousSnapshot = Clone(snapshot);
            enrolled = AddCrewMember(snapshot, userId, eventArgs.Name, eventArgs.DisplayName,
                StarshipCrewEnrollmentMethod.AutomaticAction, GetEffectiveFromSequence(snapshot));
            if (snapshot.CurrentEvent.Votes.Any(vote => string.Equals(vote.UserId, userId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Your choice for this event has already been recorded.");

            var existingConfirmation = snapshot.CurrentEvent.PendingRiskConfirmations.FirstOrDefault(item =>
                string.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase));
            if (existingConfirmation != null &&
                existingConfirmation.ExpiresAtUtc > _timeProvider.GetUtcNow() &&
                string.Equals(existingConfirmation.ChoiceId, choice.ChoiceId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("You already have a pending risk confirmation for this event.");
            if (existingConfirmation != null)
                snapshot.CurrentEvent.PendingRiskConfirmations.Remove(existingConfirmation);

            var rewardProfile = snapshot.RewardProfiles.FirstOrDefault(profile =>
                profile.Enabled && string.Equals(profile.EventType, snapshot.CurrentEvent.EventType, StringComparison.OrdinalIgnoreCase));
            if (choice.IsRisky && rewardProfile != null)
            {
                var balance = snapshot.Crew
                    .First(member => string.Equals(member.UserId, userId, StringComparison.OrdinalIgnoreCase))
                    .UnbankedBalances.FirstOrDefault(item => item.PointTypeId == rewardProfile.PointTypeId)?.Amount ?? 0;
                var riskPercent = CombinePercent(snapshot.CurrentEvent.RiskMultiplierPercent, choice.RiskMultiplierPercent);
                var (proportionalLoss, maximumLoss) = ScaleRiskLoss(rewardProfile, balance, riskPercent);
                snapshot.CurrentEvent.PendingRiskConfirmations.Add(new StarshipRiskConfirmationSnapshot
                {
                    UserId = userId,
                    ChoiceId = choice.ChoiceId,
                    ExpiresAtUtc = _timeProvider.GetUtcNow().AddSeconds(30),
                    PointTypeId = rewardProfile.PointTypeId,
                    PointTypeName = rewardProfile.PointTypeName,
                    ExposedBalance = balance,
                    MaximumLoss = Math.Min(balance, Math.Min(proportionalLoss, maximumLoss)),
                    SuccessChancePercent = GetEffectiveSuccessChance(rewardProfile, snapshot.CurrentEvent, choice),
                    RewardMultiplierPercent = CombinePercent(snapshot.CurrentEvent.RewardMultiplierPercent, choice.RewardMultiplierPercent),
                    RevealedRarity = snapshot.CurrentEvent.RevealedRarity
                });
            }
            else
            {
                snapshot.CurrentEvent.Votes.Add(new StarshipEventVoteSnapshot { UserId = userId, ChoiceId = choice.ChoiceId });
            }

            await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            result = Clone(snapshot);
        }
        finally
        {
            _stateLock.Release();
        }

        if (enrolled)
        {
            await FireTriggerAsync(DefaultCommandEventTypes.StarshipCrewJoined, result, new Dictionary<string, string>
            {
                ["User"] = eventArgs.DisplayName,
                ["CrewCount"] = result.Crew.Count.ToString(CultureInfo.InvariantCulture)
            }, eventArgs);
        }

        return (result, enrolled);
    }

    public async Task<StarshipExpeditionSnapshot> ConfirmRiskActionAsync(CommandEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var userId = RequireUserId(eventArgs);
        await EnsureLoadedAsync(cancellationToken);
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = GetActiveSnapshot();
            if (snapshot.Status != StarshipExpeditionStatus.EventOpen || snapshot.CurrentEvent == null)
                throw new InvalidOperationException("There is no open Starship event to confirm.");
            var pending = snapshot.CurrentEvent.PendingRiskConfirmations.FirstOrDefault(item =>
                string.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase));
            if (pending == null || pending.ExpiresAtUtc <= _timeProvider.GetUtcNow())
                throw new InvalidOperationException("Your risk confirmation expired. Choose the action again to review its current exposure.");
            if (snapshot.CurrentEvent.Votes.Any(vote => string.Equals(vote.UserId, userId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Your choice for this event has already been recorded.");

            var previousSnapshot = Clone(snapshot);
            snapshot.CurrentEvent.Votes.Add(new StarshipEventVoteSnapshot { UserId = userId, ChoiceId = pending.ChoiceId });
            snapshot.CurrentEvent.PendingRiskConfirmations.Remove(pending);
            await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            return Clone(snapshot);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<StarshipExpeditionSnapshot?> ShowStatusWidgetAsync(int durationSeconds, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot == null) return null;
            var previousSnapshot = Clone(_snapshot);
            var seconds = Math.Clamp(durationSeconds, 1, 3600);
            _snapshot.StatusForcedUntilUtc = _timeProvider.GetUtcNow().AddSeconds(seconds);
            await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            return Clone(_snapshot);
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
                ["Subject"] = openedEvent.Subject,
                ["Choices"] = string.Join(" | ", openedEvent.Choices.Select(choice => $"{choice.ChoiceId}: {choice.Label}")),
                ["Duration"] = current.EventVoteDurationSeconds.ToString(CultureInfo.InvariantCulture)
            });
        }

        else if (previous.CurrentEvent != null && current.CurrentEvent != null &&
                 previous.CurrentEvent.EventId == current.CurrentEvent.EventId &&
                 current.CurrentEvent.StepNumber > previous.CurrentEvent.StepNumber)
        {
            await AnnounceEventStepAsync(current, current.CurrentEvent);
            await FireTriggerAsync(DefaultCommandEventTypes.StarshipEventStepAdvanced, current, new Dictionary<string, string>
            {
                ["EventTitle"] = current.CurrentEvent.Title,
                ["EventType"] = current.CurrentEvent.EventType,
                ["Step"] = current.CurrentEvent.StepNumber.ToString(CultureInfo.InvariantCulture),
                ["Subject"] = current.CurrentEvent.Subject,
                ["Path"] = StarshipExpeditionNarrative.FormatPath(current.CurrentEvent),
                ["RevealedRarity"] = current.CurrentEvent.RevealedRarity ?? string.Empty,
                ["StepNote"] = current.CurrentEvent.StepNote,
                ["Choices"] = StarshipExpeditionNarrative.FormatChoices(current.CurrentEvent.Choices)
            });
        }

        if (current.CompletedEvents.Count > previous.CompletedEvents.Count)
        {
            var resolvedEvent = current.CompletedEvents[^1];
            var selectedChoice = resolvedEvent.Choices.FirstOrDefault(choice =>
                string.Equals(choice.ChoiceId, resolvedEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase));
            await SendChatAnnouncementAsync(StarshipExpeditionNarrative.BuildEventMessage(resolvedEvent));
            await FireTriggerAsync(DefaultCommandEventTypes.StarshipEventResolved, current, new Dictionary<string, string>
            {
                ["EventTitle"] = resolvedEvent.Title,
                ["EventType"] = resolvedEvent.EventType,
                ["Choice"] = selectedChoice?.Label ?? resolvedEvent.ResolvedChoiceId ?? string.Empty,
                ["VoteCount"] = resolvedEvent.Votes.Count.ToString(CultureInfo.InvariantCulture),
                ["Leg"] = resolvedEvent.Sequence.ToString(CultureInfo.InvariantCulture),
                ["Sector"] = resolvedEvent.Sector,
                ["RewardParticipantCount"] = resolvedEvent.RewardOutcomes.Count(outcome => outcome.EarnedAmount > 0).ToString(CultureInfo.InvariantCulture),
                ["RewardTotal"] = resolvedEvent.RewardOutcomes.Sum(outcome => outcome.EarnedAmount).ToString(CultureInfo.InvariantCulture),
                ["PointType"] = resolvedEvent.RewardOutcomes.FirstOrDefault()?.PointTypeName ?? string.Empty,
                ["Outcome"] = resolvedEvent.OutcomeResult,
                ["Rarity"] = resolvedEvent.OutcomeRarity,
                ["Story"] = resolvedEvent.OutcomeStory,
                ["Subject"] = resolvedEvent.Subject,
                ["Path"] = StarshipExpeditionNarrative.FormatPath(resolvedEvent),
                ["LostTotal"] = resolvedEvent.RewardOutcomes.Sum(outcome => outcome.LostAmount).ToString(CultureInfo.InvariantCulture),
                ["Summary"] = StarshipExpeditionNarrative.BuildEventMessage(resolvedEvent)
            });

            if (resolvedEvent.OutcomeResult is StarshipExpeditionNarrative.OutcomeSuccess or StarshipExpeditionNarrative.OutcomeFailure)
            {
                await FireTriggerAsync(
                    resolvedEvent.OutcomeResult == StarshipExpeditionNarrative.OutcomeSuccess
                        ? DefaultCommandEventTypes.StarshipEventSucceeded
                        : DefaultCommandEventTypes.StarshipEventFailed,
                    current,
                    new Dictionary<string, string>
                    {
                        ["EventTitle"] = resolvedEvent.Title,
                        ["EventType"] = resolvedEvent.EventType,
                        ["Outcome"] = resolvedEvent.OutcomeResult,
                        ["Rarity"] = resolvedEvent.OutcomeRarity,
                        ["Story"] = resolvedEvent.OutcomeStory,
                        ["Subject"] = resolvedEvent.Subject,
                        ["Path"] = StarshipExpeditionNarrative.FormatPath(resolvedEvent),
                        ["PointType"] = resolvedEvent.RewardOutcomes.FirstOrDefault()?.PointTypeName ?? string.Empty,
                        ["RewardTotal"] = resolvedEvent.RewardOutcomes.Sum(outcome => outcome.EarnedAmount).ToString(CultureInfo.InvariantCulture),
                        ["LostTotal"] = resolvedEvent.RewardOutcomes.Sum(outcome => outcome.LostAmount).ToString(CultureInfo.InvariantCulture),
                        ["AffectedCount"] = resolvedEvent.RewardOutcomes.Count(outcome => outcome.EarnedAmount > 0 || outcome.LostAmount > 0).ToString(CultureInfo.InvariantCulture)
                    });
            }

            foreach (var outcome in resolvedEvent.RewardOutcomes.Where(outcome =>
                         outcome.WasRiskyAction && outcome.Outcome is "Success" or "Failure"))
            {
                await FireTriggerAsync(
                    outcome.Outcome == "Success" ? DefaultCommandEventTypes.StarshipRiskSucceeded : DefaultCommandEventTypes.StarshipRiskFailed,
                    current,
                    new Dictionary<string, string>
                    {
                        ["User"] = outcome.DisplayName,
                        ["EventTitle"] = resolvedEvent.Title,
                        ["EventType"] = resolvedEvent.EventType,
                        ["PointType"] = outcome.PointTypeName,
                        ["EarnedAmount"] = outcome.EarnedAmount.ToString(CultureInfo.InvariantCulture),
                        ["LostAmount"] = outcome.LostAmount.ToString(CultureInfo.InvariantCulture),
                        ["Rarity"] = outcome.Rarity,
                        ["Choice"] = selectedChoice?.Label ?? resolvedEvent.ResolvedChoiceId ?? string.Empty,
                        ["SuccessChancePercent"] = outcome.SuccessChancePercent.ToString(CultureInfo.InvariantCulture),
                        ["Outcome"] = outcome.Outcome
                    },
                    new CommandEventArgs
                    {
                        Command = DefaultCommandName,
                        Name = current.Crew.First(member => member.UserId == outcome.UserId).UserName,
                        DisplayName = outcome.DisplayName,
                        UserId = outcome.UserId
                    });
            }
        }

        if (IsTerminal(current.Status) && !IsTerminal(previous.Status))
        {
            await AnnounceVoyageEndedAsync(current);
            await FireVoyageEndedTriggerAsync(current, current.Status == StarshipExpeditionStatus.Aborted ? current.StartedBy : null);
        }
    }

    private async Task ProcessPendingBankedPayoutsAsync(CancellationToken cancellationToken)
    {
        if (pointsSystem == null) return;
        await _payoutLock.WaitAsync(cancellationToken);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                StarshipExpeditionSnapshot? snapshot;
                StarshipBankedPayoutSnapshot? payout;
                await _stateLock.WaitAsync(cancellationToken);
                try
                {
                    payout = _snapshot?.BankedPayouts.FirstOrDefault(item => !item.IsPaid);
                    snapshot = _snapshot == null ? null : Clone(_snapshot);
                    if (payout != null)
                        payout = snapshot!.BankedPayouts.First(item => item.OperationId == payout.OperationId);
                }
                finally
                {
                    _stateLock.Release();
                }

                if (snapshot == null || payout == null) break;

                try
                {
                    var settled = await pointsSystem.AddPointsByUserIdOnce(payout.UserId, payout.PointTypeId, payout.Amount, payout.OperationId);
                    if (!settled)
                        throw new InvalidOperationException($"The banked award for {payout.UserId} was not settled.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to bank Starship payout {OperationId}; it will be retried", payout.OperationId);
                    break;
                }

                StarshipExpeditionSnapshot? updatedSnapshot = null;
                await _stateLock.WaitAsync(cancellationToken);
                try
                {
                    var livePayout = _snapshot?.BankedPayouts.FirstOrDefault(item => item.OperationId == payout.OperationId);
                    if (livePayout != null && !livePayout.IsPaid)
                    {
                        var previousSnapshot = Clone(_snapshot!);
                        livePayout.IsPaid = true;
                        livePayout.PaidAtUtc = _timeProvider.GetUtcNow();
                        await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
                        updatedSnapshot = Clone(_snapshot!);
                    }
                }
                finally
                {
                    _stateLock.Release();
                }

                if (updatedSnapshot != null)
                {
                    await FireTriggerAsync(DefaultCommandEventTypes.StarshipCrewRewardBanked, updatedSnapshot, new Dictionary<string, string>
                    {
                        ["User"] = payout.DisplayName,
                        ["PointType"] = payout.PointTypeName,
                        ["Amount"] = payout.Amount.ToString(CultureInfo.InvariantCulture),
                        ["EventId"] = payout.EventId.ToString("N")
                    },
                    new CommandEventArgs
                    {
                        Command = DefaultCommandName,
                        Name = payout.UserName,
                        DisplayName = payout.DisplayName,
                        UserId = payout.UserId
                    });
                }
            }
        }
        finally
        {
            _payoutLock.Release();
        }
    }

    private Task AnnounceEventOpenedAsync(StarshipExpeditionSnapshot snapshot, StarshipExpeditionEventSnapshot currentEvent)
    {
        var choices = StarshipExpeditionNarrative.FormatChoices(currentEvent.Choices);
        var message = $"Starship vote: {currentEvent.Title} at {currentEvent.Sector}. {currentEvent.Description} {choices} Vote with !sv <number> within {snapshot.EventVoteDurationSeconds}s.";
        return SendChatAnnouncementAsync(message);
    }

    private Task AnnounceEventStepAsync(StarshipExpeditionSnapshot snapshot, StarshipExpeditionEventSnapshot currentEvent)
    {
        var choices = StarshipExpeditionNarrative.FormatChoices(currentEvent.Choices);
        var rarity = string.IsNullOrEmpty(currentEvent.RevealedRarity) ? string.Empty : $" Known rarity: {currentEvent.RevealedRarity}.";
        return SendChatAnnouncementAsync(
            $"{currentEvent.Title} - step {currentEvent.StepNumber}. {currentEvent.StepNote}{rarity} {choices} Vote with !sv <number> within {snapshot.EventVoteDurationSeconds}s.");
    }

    private async Task AnnounceVoyageEndedAsync(StarshipExpeditionSnapshot snapshot)
    {
        if (snapshot.AnnounceSessionStatusInChat)
        {
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

        await SendChatAnnouncementAsync(string.Join(" ", StarshipExpeditionNarrative.BuildVoyageLines(snapshot)));
    }

    private Task FireVoyageEndedTriggerAsync(StarshipExpeditionSnapshot snapshot, string? triggeredBy) =>
        FireTriggerAsync(DefaultCommandEventTypes.StarshipVoyageEnded, snapshot, new Dictionary<string, string>
        {
            ["Result"] = snapshot.Status.ToString(),
            ["Duration"] = Math.Max(0, (long)(_timeProvider.GetUtcNow() - snapshot.StartedAtUtc).TotalSeconds).ToString(CultureInfo.InvariantCulture),
            ["TriggeredBy"] = triggeredBy ?? snapshot.StartedBy,
            ["Summary"] = string.Join(" ", StarshipExpeditionNarrative.BuildVoyageLines(snapshot)),
            ["EventsSucceeded"] = snapshot.CompletedEvents.Count(item => item.OutcomeResult == StarshipExpeditionNarrative.OutcomeSuccess).ToString(CultureInfo.InvariantCulture),
            ["EventsFailed"] = snapshot.CompletedEvents.Count(item => item.OutcomeResult == StarshipExpeditionNarrative.OutcomeFailure).ToString(CultureInfo.InvariantCulture)
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

    private static bool IsSameTransitionPoint(StarshipExpeditionSnapshot current, StarshipExpeditionSnapshot observed) =>
        current.ExpeditionId == observed.ExpeditionId &&
        current.Status == observed.Status &&
        current.CurrentLeg == observed.CurrentLeg &&
        current.NextEventSequence == observed.NextEventSequence &&
        current.NextEventAtUtc == observed.NextEventAtUtc &&
        current.CurrentEvent?.EventId == observed.CurrentEvent?.EventId &&
        current.CurrentEvent?.StepNumber == observed.CurrentEvent?.StepNumber;

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

    private async Task<List<StarshipRewardProfile>> SnapshotRewardProfilesAsync(
        IReadOnlyList<StarshipRewardProfile> rewardProfiles,
        CancellationToken cancellationToken)
    {
        var profiles = JsonSerializer.Deserialize<List<StarshipRewardProfile>>(
            JsonSerializer.Serialize(rewardProfiles, JsonOptions), JsonOptions) ?? [];
        foreach (var profile in profiles.Where(profile => profile.Enabled))
        {
            if (pointsSystem == null)
                throw new InvalidOperationException("Starship reward profiles require the points system.");
            var pointType = await pointsSystem.GetPointTypeById(profile.PointTypeId);
            if (pointType?.Id == null)
                throw new InvalidOperationException($"Point type {profile.PointTypeId} for {profile.EventType} no longer exists.");
            profile.PointTypeName = pointType.Name;
        }
        return profiles;
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
        var observedSnapshot = await GetSnapshotAsync(cancellationToken);
        if (observedSnapshot == null || IsTerminal(observedSnapshot.Status)) return;
        var now = _timeProvider.GetUtcNow();
        var boardingDue = observedSnapshot.Status == StarshipExpeditionStatus.Boarding && observedSnapshot.BoardingClosesAtUtc <= now;
        var eventDue = observedSnapshot.CurrentEvent is { } observedEvent && observedEvent.ClosesAtUtc <= now;
        var nextEventDue = observedSnapshot.Status == StarshipExpeditionStatus.Cruising && observedSnapshot.NextEventAtUtc <= now;
        if (!boardingDue && !eventDue && !nextEventDue)
        {
            await ProcessPendingBankedPayoutsAsync(cancellationToken);
            return;
        }

        var profile = eventDue && observedSnapshot.CurrentEvent != null
            ? GetRewardProfile(observedSnapshot, observedSnapshot.CurrentEvent.EventType)
            : null;
        var eligibleUserIds = profile == null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : await GetEligibleCrewUserIdsAsync(observedSnapshot, cancellationToken);

        StarshipExpeditionSnapshot? previousSnapshot = null;
        StarshipExpeditionSnapshot? updatedSnapshot = null;
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = _snapshot;
            if (snapshot == null || !IsSameTransitionPoint(snapshot, observedSnapshot)) return;

            previousSnapshot = Clone(snapshot);
            if (boardingDue && snapshot.Status == StarshipExpeditionStatus.Boarding)
            {
                snapshot.BoardingClosesAtUtc = null;
                if (snapshot.Crew.Count == 0)
                {
                    snapshot.Status = StarshipExpeditionStatus.Aborted;
                    snapshot.NextEventAtUtc = null;
                    AppendVoyageEndedActivity(snapshot, now);
                }
                else
                {
                    OpenNextEventLocked(snapshot, now);
                }
            }
            else if (eventDue && snapshot.CurrentEvent is { } currentEvent && currentEvent.EventId == observedSnapshot.CurrentEvent!.EventId)
            {
                ResolveCurrentEventAndRewardsLocked(snapshot, now, eligibleUserIds);
            }
            else if (nextEventDue && snapshot.Status == StarshipExpeditionStatus.Cruising)
            {
                OpenNextEventLocked(snapshot, now);
            }
            else
                return;

            await PersistMutationWithRollbackAsync(previousSnapshot, cancellationToken);
            updatedSnapshot = Clone(snapshot);
        }
        finally
        {
            _stateLock.Release();
        }

        if (previousSnapshot != null && updatedSnapshot != null)
            await NotifyTransitionAsync(previousSnapshot, updatedSnapshot);
        await ProcessPendingBankedPayoutsAsync(cancellationToken);
    }

    private void OpenNextEventLocked(StarshipExpeditionSnapshot snapshot, DateTimeOffset now)
    {
        snapshot.BoardingClosesAtUtc = null;
        if (snapshot.CurrentLeg >= snapshot.LegCount)
        {
            snapshot.Status = StarshipExpeditionStatus.Returned;
            snapshot.NextEventAtUtc = null;
            snapshot.CurrentEvent = null;
            AppendVoyageEndedActivity(snapshot, now);
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
            Subject = StarshipExpeditionStory.GenerateSubject(snapshot.Seed, sequence, definition.EventType),
            ObjectKind = StarshipExpeditionStory.GetObjectKind(definition.EventType),
            ObjectVariant = StarshipExpeditionStory.StableIndex(snapshot.Seed, sequence, "object-variant", StarshipExpeditionStory.ObjectVariantCount),
            ObjectRoll = StarshipExpeditionStory.StableIndex(snapshot.Seed, sequence, "object-roll", int.MaxValue),
            SuccessStory = definition.SuccessStory,
            FailureStory = definition.FailureStory,
            Sector = snapshot.CurrentSector,
            OpenedAtUtc = now,
            ClosesAtUtc = now.AddSeconds(snapshot.EventVoteDurationSeconds),
            Choices = FilterChoices(definition.Choices, []),
            ChoicePool = [.. definition.Choices],
            StepNumber = 1,
            DefaultChoiceId = definition.DefaultChoiceId,
            Votes = []
        };
        snapshot.NextEventSequence++;
        snapshot.CurrentEvent.Description = StarshipExpeditionStory.Render(
            definition.Description, snapshot.CurrentEvent, snapshot.Seed, [], null);
        AppendActivityLocked(snapshot, StarshipActivityKinds.EventOpened,
            snapshot.CurrentEvent.Subject.Length == 0 ? snapshot.CurrentEvent.Title : $"{snapshot.CurrentEvent.Title}: {snapshot.CurrentEvent.Subject}",
            string.Empty, now);
        snapshot.Status = StarshipExpeditionStatus.EventOpen;
        snapshot.NextEventAtUtc = null;
    }

    private static List<StarshipEventChoiceSnapshot> FilterChoices(
        IEnumerable<StarshipEventChoiceSnapshot> pool,
        ICollection<string> flags) =>
        pool.Where(choice => string.IsNullOrEmpty(choice.GrantsFlag) ||
                             !flags.Contains(choice.GrantsFlag, StringComparer.OrdinalIgnoreCase))
            .ToList();

    private static void AdvanceEventStepLocked(
        StarshipExpeditionSnapshot snapshot,
        StarshipExpeditionEventSnapshot currentEvent,
        StarshipEventChoiceSnapshot choice,
        int voteCount,
        IReadOnlyList<string> voterNames,
        DateTimeOffset now)
    {
        currentEvent.Path.Add(new StarshipEventPathStepSnapshot
        {
            StepNumber = currentEvent.StepNumber,
            ChoiceId = choice.ChoiceId,
            Label = choice.Label,
            VoteCount = voteCount
        });
        if (!string.IsNullOrEmpty(choice.GrantsFlag)) currentEvent.Flags.Add(choice.GrantsFlag);
        currentEvent.SuccessChanceModifierPercent += choice.SuccessChanceModifierPercent;
        currentEvent.RewardMultiplierPercent = CombinePercent(currentEvent.RewardMultiplierPercent, choice.RewardMultiplierPercent);
        currentEvent.RiskMultiplierPercent = CombinePercent(currentEvent.RiskMultiplierPercent, choice.RiskMultiplierPercent);

        var note = new List<string>();
        if (choice.RevealsRarity)
        {
            var profile = snapshot.RewardProfiles.FirstOrDefault(item => item.Enabled &&
                string.Equals(item.EventType, currentEvent.EventType, StringComparison.OrdinalIgnoreCase));
            if (profile != null)
                currentEvent.RevealedRarity = ChooseEventRarity(snapshot, currentEvent, profile);
        }

        var story = StarshipExpeditionStory.Render(choice.Story, currentEvent, snapshot.Seed, voterNames, currentEvent.RevealedRarity);
        note.Add(story.Length > 0 ? story : $"Crew chose: {choice.Label}.");
        if (choice.RevealsRarity)
        {
            note.Add(currentEvent.RevealedRarity == null
                ? "The scan finds nothing of value."
                : $"Scan complete: this is a {currentEvent.RevealedRarity} find.");
        }

        var effects = StarshipExpeditionNarrative.DescribeEffects(choice);
        if (!choice.RevealsRarity && effects.Length > 0) note.Add($"Effect: {effects}.");
        currentEvent.StepNote = string.Join(" ", note);
        AppendActivityLocked(snapshot, StarshipActivityKinds.StepAdvanced, currentEvent.StepNote, string.Join(", ", voterNames), now);
        if (choice.RevealsRarity && currentEvent.RevealedRarity != null)
            AppendActivityLocked(snapshot, StarshipActivityKinds.RarityRevealed, $"{currentEvent.RevealedRarity} find revealed", string.Empty, now);
        currentEvent.StepNumber++;
        currentEvent.Votes.Clear();
        currentEvent.PendingRiskConfirmations.Clear();
        currentEvent.Choices = FilterChoices(currentEvent.ChoicePool, currentEvent.Flags);
        currentEvent.OpenedAtUtc = now;
        currentEvent.ClosesAtUtc = now.AddSeconds(snapshot.EventVoteDurationSeconds);
    }

    /// <returns><c>true</c> when the event finished; <c>false</c> when it advanced to another step.</returns>
    private bool ResolveCurrentEventLocked(StarshipExpeditionSnapshot snapshot, DateTimeOffset now)
    {
        var currentEvent = snapshot.CurrentEvent ?? throw new InvalidOperationException("There is no open event to resolve.");
        var voteCounts = currentEvent.Votes
            .GroupBy(vote => vote.ChoiceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { ChoiceId = group.Key, Count = group.Count() })
            .ToList();
        var maxVotes = voteCounts.Count == 0 ? 0 : voteCounts.Max(vote => vote.Count);
        var leaders = voteCounts.Where(vote => vote.Count == maxVotes).Select(vote => vote.ChoiceId).ToList();
        var winningId = leaders.Count == 1
            ? leaders[0]
            : currentEvent.DefaultChoiceId;
        var winningChoice = currentEvent.Choices.FirstOrDefault(choice =>
            string.Equals(choice.ChoiceId, winningId, StringComparison.OrdinalIgnoreCase));
        if (winningChoice is { ContinuesEvent: true })
        {
            AdvanceEventStepLocked(snapshot, currentEvent, winningChoice,
                voteCounts.FirstOrDefault(vote => string.Equals(vote.ChoiceId, winningId, StringComparison.OrdinalIgnoreCase))?.Count ?? 0,
                StarshipExpeditionStory.GetVoterNames(snapshot, currentEvent, winningId), now);
            return false;
        }

        currentEvent.ResolvedChoiceId = winningId;
        currentEvent.ResolvedAtUtc = now;
        currentEvent.PendingRiskConfirmations.Clear();
        currentEvent.Path.Add(new StarshipEventPathStepSnapshot
        {
            StepNumber = currentEvent.StepNumber,
            ChoiceId = winningId,
            Label = winningChoice?.Label ?? winningId,
            VoteCount = voteCounts.FirstOrDefault(vote => string.Equals(vote.ChoiceId, winningId, StringComparison.OrdinalIgnoreCase))?.Count ?? 0
        });
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
            return true;
        }

        snapshot.Status = StarshipExpeditionStatus.Cruising;
        var interval = GetDeterministicIntervalSeconds(snapshot);
        snapshot.NextEventAtUtc = now.AddSeconds(interval);
        return true;
    }

    private void ResolveCurrentEventAndRewardsLocked(
        StarshipExpeditionSnapshot snapshot,
        DateTimeOffset now,
        IReadOnlySet<string> eligibleUserIds)
    {
        var eventType = snapshot.CurrentEvent?.EventType;
        var profile = snapshot.RewardProfiles.FirstOrDefault(item => item.Enabled &&
            string.Equals(item.EventType, eventType, StringComparison.OrdinalIgnoreCase));
        if (!ResolveCurrentEventLocked(snapshot, now)) return;
        var resolvedEvent = snapshot.CompletedEvents[^1];
        resolvedEvent.EligibleRewardUserIds.AddRange(eligibleUserIds.Order(StringComparer.OrdinalIgnoreCase));

        if (profile != null)
            ApplyRewardProfile(snapshot, resolvedEvent, profile, eligibleUserIds);
        resolvedEvent.OutcomeStory = StarshipExpeditionStory.BuildOutcomeStory(snapshot, resolvedEvent);
        AppendActivityLocked(snapshot, StarshipActivityKinds.EventResolved, StarshipExpeditionNarrative.BuildEventMessage(resolvedEvent), string.Empty, now);
        if (snapshot.Status == StarshipExpeditionStatus.Returned)
            AppendVoyageEndedActivity(snapshot, now);

        if (resolvedEvent.EventType == "StationStop" || snapshot.Status == StarshipExpeditionStatus.Returned)
            QueuePayouts(snapshot, resolvedEvent.EventId);
    }

    private async Task<HashSet<string>> GetEligibleCrewUserIdsAsync(
        StarshipExpeditionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var eventSequence = snapshot.CurrentEvent?.Sequence ?? int.MaxValue;
        var effectiveCrew = snapshot.Crew.Where(member => member.EffectiveFromSequence <= eventSequence).ToList();
        if (!snapshot.ActiveCrewRewardsOnly)
            return effectiveCrew.Select(member => member.UserId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (viewerFeature == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var eligibleNames = viewerFeature.GetActiveViewers().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (PointsSystem.IncludeSubsInActive)
        {
            foreach (var viewerName in viewerFeature.GetCurrentViewers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!eligibleNames.Contains(viewerName) && await viewerFeature.IsSubscriber(viewerName))
                    eligibleNames.Add(viewerName);
            }
        }

        return effectiveCrew
            .Where(member => eligibleNames.Contains(member.UserName) || eligibleNames.Contains(member.DisplayName))
            .Select(member => member.UserId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void ApplyRewardProfile(
        StarshipExpeditionSnapshot snapshot,
        StarshipExpeditionEventSnapshot resolvedEvent,
        StarshipRewardProfile profile,
        IReadOnlySet<string> eligibleUserIds)
    {
        var finalChoice = resolvedEvent.Choices.FirstOrDefault(choice =>
            string.Equals(choice.ChoiceId, resolvedEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase));
        var committed = finalChoice?.IsRisky == true;
        var offersCommit = resolvedEvent.ChoicePool.Concat(resolvedEvent.Choices).Any(choice => choice.IsRisky);
        if (finalChoice?.EndsWithoutReward == true || (!committed && offersCommit))
        {
            resolvedEvent.OutcomeResult = StarshipExpeditionNarrative.OutcomePassed;
            return;
        }

        var chance = GetEffectiveSuccessChance(profile, resolvedEvent, finalChoice);
        var succeeded = GetDeterministicIndex(snapshot.Seed, resolvedEvent.Sequence,
            $"reward-success:{resolvedEvent.EventId:N}", 100) < chance;
        var rarity = ChooseEventRarity(snapshot, resolvedEvent, profile);
        var rewardPercent = CombinePercent(resolvedEvent.RewardMultiplierPercent, finalChoice?.RewardMultiplierPercent ?? 100);
        var riskPercent = CombinePercent(resolvedEvent.RiskMultiplierPercent, finalChoice?.RiskMultiplierPercent ?? 100);
        resolvedEvent.OutcomeResult = succeeded ? StarshipExpeditionNarrative.OutcomeSuccess : StarshipExpeditionNarrative.OutcomeFailure;
        resolvedEvent.OutcomeRarity = rarity;

        if (committed)
        {
            var voterIds = resolvedEvent.Votes
                .Where(vote => string.Equals(vote.ChoiceId, resolvedEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase))
                .Select(vote => vote.UserId)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var voterId in voterIds)
            {
                var member = snapshot.Crew.FirstOrDefault(candidate =>
                    string.Equals(candidate.UserId, voterId, StringComparison.OrdinalIgnoreCase));
                if (member != null)
                    ApplyRewardToParticipant(snapshot, resolvedEvent, profile, member, true, eligibleUserIds.Contains(member.UserId),
                        succeeded, rarity, chance, rewardPercent, riskPercent);
            }
            return;
        }

        foreach (var member in snapshot.Crew.Where(member => eligibleUserIds.Contains(member.UserId)))
            ApplyRewardToParticipant(snapshot, resolvedEvent, profile, member, false, true,
                succeeded, rarity, chance, rewardPercent, riskPercent);
    }

    private static int GetEffectiveSuccessChance(
        StarshipRewardProfile profile,
        StarshipExpeditionEventSnapshot expeditionEvent,
        StarshipEventChoiceSnapshot? choice) =>
        Math.Clamp(profile.SuccessChancePercent + expeditionEvent.SuccessChanceModifierPercent +
                   (choice?.SuccessChanceModifierPercent ?? 0), 0, 100);

    private static int CombinePercent(int first, int second) =>
        Math.Clamp((int)((long)first * second / 100), 0, 1000);

    private static void ApplyRewardToParticipant(
        StarshipExpeditionSnapshot snapshot,
        StarshipExpeditionEventSnapshot resolvedEvent,
        StarshipRewardProfile profile,
        StarshipCrewMemberSnapshot member,
        bool isRisky,
        bool isEligible,
        bool succeeded,
        string rarity,
        int successChancePercent,
        int rewardPercent,
        int riskPercent)
    {
        if (isRisky && !isEligible)
        {
            resolvedEvent.RewardOutcomes.Add(new StarshipRewardOutcomeSnapshot
            {
                UserId = member.UserId,
                DisplayName = member.DisplayName,
                PointTypeId = profile.PointTypeId,
                PointTypeName = profile.PointTypeName,
                Outcome = "Ineligible",
                WasRiskyAction = true,
                SuccessChancePercent = successChancePercent
            });
            return;
        }

        long earnedAmount = 0;
        long lostAmount = 0;
        var outcome = succeeded ? "Success" : "Failure";

        var currentBalance = member.UnbankedBalances.FirstOrDefault(balance => balance.PointTypeId == profile.PointTypeId);
        if (!succeeded && currentBalance != null)
        {
            var (proportionalLoss, maximumLoss) = ScaleRiskLoss(profile, currentBalance.Amount, riskPercent);
            lostAmount = Math.Min(currentBalance.Amount, Math.Min(proportionalLoss, maximumLoss));
            currentBalance.Amount -= lostAmount;
        }

        if (succeeded && isEligible)
        {
            var (minimum, maximum) = GetRarityRange(profile, rarity);
            var range = checked((int)Math.Min(int.MaxValue, maximum - minimum + 1));
            var rolledAmount = minimum + GetDeterministicIndex(snapshot.Seed, resolvedEvent.Sequence,
                $"reward-amount:{resolvedEvent.EventId:N}:{member.UserId}:{rarity}", Math.Max(1, range));
            rolledAmount = (long)((decimal)rolledAmount * rewardPercent / 100m);
            var earnedThisVoyage = snapshot.CompletedEvents
                .SelectMany(expeditionEvent => expeditionEvent.RewardOutcomes)
                .Where(outcome => string.Equals(outcome.UserId, member.UserId, StringComparison.OrdinalIgnoreCase) &&
                                  outcome.PointTypeId == profile.PointTypeId)
                .Sum(outcome => outcome.EarnedAmount);
            var availableVoyageCap = Math.Max(0, profile.MaximumUnbankedPerVoyage - earnedThisVoyage);
            earnedAmount = Math.Min(rolledAmount, Math.Min(profile.MaximumPerEvent, availableVoyageCap));
            if (earnedAmount > 0)
            {
                currentBalance ??= AddUnbankedBalance(member, profile);
                currentBalance.Amount += earnedAmount;
            }
        }
        else if (succeeded)
        {
            outcome = "Ineligible";
        }

        resolvedEvent.RewardOutcomes.Add(new StarshipRewardOutcomeSnapshot
        {
            UserId = member.UserId,
            DisplayName = member.DisplayName,
            PointTypeId = profile.PointTypeId,
            PointTypeName = profile.PointTypeName,
            Outcome = outcome,
            Rarity = rarity,
            WasRiskyAction = isRisky,
            SuccessChancePercent = successChancePercent,
            EarnedAmount = earnedAmount,
            LostAmount = lostAmount
        });
    }

    private static (long Proportional, long Maximum) ScaleRiskLoss(StarshipRewardProfile profile, long balance, int riskPercent)
    {
        var lossPercent = Math.Min(100m, profile.RiskLossPercent * riskPercent / 100m);
        return ((long)(balance * lossPercent / 100m), (long)((decimal)profile.MaximumRiskLossPerEvent * riskPercent / 100m));
    }

    private static string ChooseEventRarity(
        StarshipExpeditionSnapshot snapshot,
        StarshipExpeditionEventSnapshot expeditionEvent,
        StarshipRewardProfile profile)
    {
        var roll = GetDeterministicIndex(snapshot.Seed, expeditionEvent.Sequence,
            $"reward-rarity:{expeditionEvent.EventId:N}", 100);
        if (roll < profile.CommonWeightPercent) return "Common";
        if (roll < profile.CommonWeightPercent + profile.UncommonWeightPercent) return "Uncommon";
        return "Rare";
    }

    private static (long Minimum, long Maximum) GetRarityRange(StarshipRewardProfile profile, string rarity) => rarity switch
    {
        "Uncommon" => (profile.UncommonMinimum, profile.UncommonMaximum),
        "Rare" => (profile.RareMinimum, profile.RareMaximum),
        _ => (profile.CommonMinimum, profile.CommonMaximum)
    };

    private static StarshipUnbankedBalanceSnapshot AddUnbankedBalance(StarshipCrewMemberSnapshot member, StarshipRewardProfile profile)
    {
        var balance = new StarshipUnbankedBalanceSnapshot
        {
            PointTypeId = profile.PointTypeId,
            PointTypeName = profile.PointTypeName,
            Amount = 0
        };
        member.UnbankedBalances.Add(balance);
        return balance;
    }

    private static void QueuePayouts(StarshipExpeditionSnapshot snapshot, Guid payoutEventId)
    {
        foreach (var member in snapshot.Crew)
        {
            foreach (var balance in member.UnbankedBalances.Where(balance => balance.Amount > 0))
            {
                var operationId = $"starship:{snapshot.ExpeditionId:N}:{payoutEventId:N}:{member.UserId}:{balance.PointTypeId}";
                if (!snapshot.BankedPayouts.Any(payout => string.Equals(payout.OperationId, operationId, StringComparison.Ordinal)))
                {
                    snapshot.BankedPayouts.Add(new StarshipBankedPayoutSnapshot
                    {
                        OperationId = operationId,
                        EventId = payoutEventId,
                        UserId = member.UserId,
                        UserName = member.UserName,
                        DisplayName = member.DisplayName,
                        PointTypeId = balance.PointTypeId,
                        PointTypeName = balance.PointTypeName,
                        Amount = balance.Amount
                    });
                }
                balance.Amount = 0;
            }
        }
    }

    private async Task PersistAndBroadcastLockedAsync(CancellationToken cancellationToken)
    {
        if (_snapshot == null) return;
        _snapshot.UpdatedAtUtc = _timeProvider.GetUtcNow();
        await stateStore.SaveAsync(_snapshot, cancellationToken);
        await BroadcastLockedAsync(cancellationToken);
    }

    private async Task BroadcastLockedAsync(CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = "expedition_state",
            snapshot = _snapshot == null ? null : StarshipExpeditionOverlaySnapshot.From(_snapshot, _timeProvider.GetUtcNow()),
            appearance = await ResolveAppearanceAsync(cancellationToken)
        }, JsonOptions);
        try
        {
            await webSocketMessenger.AddToQueue(WsTopics.Starship, payload);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed broadcasting Starship Expedition state {ExpeditionId}", _snapshot?.ExpeditionId);
        }
    }

    private async Task<StarshipAppearanceSettings> ResolveAppearanceAsync(CancellationToken cancellationToken)
    {
        if (_appearance != null) return _appearance;
        try
        {
            _appearance = (await settingsStore.LoadAsync(cancellationToken)).Appearance;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not load Starship overlay appearance; using defaults");
            return new StarshipAppearanceSettings();
        }

        return _appearance;
    }

    public Task<StarshipAppearanceSettings> GetAppearanceAsync(CancellationToken cancellationToken = default) =>
        ResolveAppearanceAsync(cancellationToken);

    public async Task RefreshAppearanceAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            _appearance = null;
            await BroadcastLockedAsync(cancellationToken);
        }
        finally
        {
            _stateLock.Release();
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

    private static StarshipRewardProfile? GetRewardProfile(StarshipExpeditionSnapshot snapshot, string eventType) =>
        snapshot.RewardProfiles.FirstOrDefault(profile => profile.Enabled &&
            string.Equals(profile.EventType, eventType, StringComparison.OrdinalIgnoreCase));

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

    private static int GetDeterministicIndex(int seed, int sequence, string purpose, int count) =>
        StarshipExpeditionStory.StableIndex(seed, sequence, purpose, count);

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
        StarshipCrewEnrollmentMethod enrollmentMethod,
        int effectiveFromSequence = 0)
    {
        if (snapshot.Crew.Any(member => string.Equals(member.UserId, userId, StringComparison.OrdinalIgnoreCase))) return false;
        var now = _timeProvider.GetUtcNow();
        snapshot.Crew.Add(new StarshipCrewMemberSnapshot
        {
            UserId = userId,
            UserName = string.IsNullOrWhiteSpace(userName) ? displayName ?? userId : userName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? userName ?? userId : displayName,
            JoinedAtUtc = now,
            EnrollmentMethod = enrollmentMethod,
            EffectiveFromSequence = effectiveFromSequence
        });
        var joined = snapshot.Crew[^1];
        AppendActivityLocked(snapshot, StarshipActivityKinds.CrewJoined,
            $"{joined.DisplayName} joined the crew - {snapshot.Crew.Count} aboard", joined.DisplayName, now);
        return true;
    }

    private static void AppendActivityLocked(StarshipExpeditionSnapshot snapshot, string kind, string text, string actor, DateTimeOffset now)
    {
        snapshot.ActivityFeed.Add(new StarshipActivityItem
        {
            Id = snapshot.NextActivityId++,
            AtUtc = now,
            Kind = kind,
            Text = text,
            Actor = actor
        });
        var excess = snapshot.ActivityFeed.Count - StarshipExpeditionOverlaySnapshot.MaximumActivityItems;
        if (excess > 0) snapshot.ActivityFeed.RemoveRange(0, excess);
    }

    private static void AppendVoyageEndedActivity(StarshipExpeditionSnapshot snapshot, DateTimeOffset now) =>
        AppendActivityLocked(snapshot, StarshipActivityKinds.VoyageEnded, StarshipExpeditionNarrative.BuildVoyageLines(snapshot)[0], string.Empty, now);

    private static int GetEffectiveFromSequence(StarshipExpeditionSnapshot snapshot) =>
        snapshot.CurrentEvent == null ? 0 : snapshot.CurrentEvent.Sequence + 1;

    private static string DescribePendingShare(StarshipExpeditionSnapshot snapshot, string userId)
    {
        var member = snapshot.Crew.FirstOrDefault(item => string.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase));
        return member != null && snapshot.CurrentEvent != null && member.EffectiveFromSequence > snapshot.CurrentEvent.Sequence
            ? " Your vote counts now; you share rewards and losses from the next event."
            : string.Empty;
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
        string DefaultChoiceId,
        string SuccessStory = "",
        string FailureStory = "");
}