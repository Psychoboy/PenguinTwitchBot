namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed class StarshipMessageSettings
{
    // Defaults matching standard Starship Expedition responses
    public const string DefaultJoinSuccess = "You joined the Starship Expedition crew ({crewCount} aboard).{shareNote}";
    public const string DefaultJoinSuccessEventOpen = "You joined the Starship Expedition crew ({crewCount} aboard). Vote on the current event with !sv <number>! You share rewards and losses from the next event.";
    public const string DefaultAlreadyAboard = "You are already aboard the Starship Expedition crew ({crewCount} aboard).";
    public const string DefaultAlreadyAboardEventOpen = "You are already aboard the Starship Expedition crew! Vote on the current event with !sv <number>.";
    public const string DefaultAlreadyVotedEventOpen = "You are already aboard the Starship Expedition crew and your choice for this event has already been recorded.";

    public const string DefaultVoteRecorded = "Your Starship vote was recorded: {choice} ({tally} vote{plural} so far).{enrollNote}";
    public const string DefaultVotePrompt = "Choose one of the current Starship event options: !sv <number>";
    public const string DefaultConfirmNotNeeded = "No risk confirmation is needed; votes count immediately when using !sv <number>.";
    public const string DefaultConfirmSuccess = "Confirmed and committed: {choice}. Results are announced when the vote ends.";

    public const string DefaultStartNotAuthorized = "Only a moderator or broadcaster can start the Starship Expedition.";
    public const string DefaultStartSuccess = "Starship Expedition boarding is open for {duration} seconds.";
    public const string DefaultStopNotAuthorized = "Only a moderator or broadcaster can stop the Starship Expedition.";
    public const string DefaultStopNoneActive = "No Starship Expedition is active.";
    public const string DefaultStopAborted = "Starship Expedition aborted.";
    public const string DefaultAdvanceNotAuthorized = "Only a moderator or broadcaster can advance the Starship Expedition.";

    public const string DefaultHelp = "Use !sj to join or !sv <number> to vote (long forms: !starship join|vote). Moderators: !starship start [legs] [min minutes] [max minutes], !starship advance, or !starship stop.";

    public const string DefaultEventOpened = "Starship vote: {title} at {sector}. {description} {choices} Vote with !sv <number> within {duration}s.";
    public const string DefaultEventStepAdvanced = "{title}. {note}{rarity} {choices} Vote with !sv <number> within {duration}s.";
    public const string DefaultEventResolved = "{title} resolved: {outcome} Results: {status}";

    // Crew Join Responses
    public string JoinSuccess { get; set; } = DefaultJoinSuccess;
    public string JoinSuccessEventOpen { get; set; } = DefaultJoinSuccessEventOpen;
    public string AlreadyAboard { get; set; } = DefaultAlreadyAboard;
    public string AlreadyAboardEventOpen { get; set; } = DefaultAlreadyAboardEventOpen;
    public string AlreadyVotedEventOpen { get; set; } = DefaultAlreadyVotedEventOpen;

    // Voting Responses
    public string VoteRecorded { get; set; } = DefaultVoteRecorded;
    public string VotePrompt { get; set; } = DefaultVotePrompt;
    public string ConfirmNotNeeded { get; set; } = DefaultConfirmNotNeeded;
    public string ConfirmSuccess { get; set; } = DefaultConfirmSuccess;

    // Moderator & Control Responses
    public string StartNotAuthorized { get; set; } = DefaultStartNotAuthorized;
    public string StartSuccess { get; set; } = DefaultStartSuccess;
    public string StopNotAuthorized { get; set; } = DefaultStopNotAuthorized;
    public string StopNoneActive { get; set; } = DefaultStopNoneActive;
    public string StopAborted { get; set; } = DefaultStopAborted;
    public string AdvanceNotAuthorized { get; set; } = DefaultAdvanceNotAuthorized;

    // Help & Information
    public string Help { get; set; } = DefaultHelp;

    // Event Chat Announcements
    public string EventOpened { get; set; } = DefaultEventOpened;
    public string EventStepAdvanced { get; set; } = DefaultEventStepAdvanced;
    public string EventResolved { get; set; } = DefaultEventResolved;

    public static StarshipMessageSettings Normalize(StarshipMessageSettings? settings)
    {
        settings ??= new StarshipMessageSettings();
        settings.JoinSuccess = string.IsNullOrWhiteSpace(settings.JoinSuccess) ? DefaultJoinSuccess : settings.JoinSuccess.Trim();
        settings.JoinSuccessEventOpen = string.IsNullOrWhiteSpace(settings.JoinSuccessEventOpen) ? DefaultJoinSuccessEventOpen : settings.JoinSuccessEventOpen.Trim();
        settings.AlreadyAboard = string.IsNullOrWhiteSpace(settings.AlreadyAboard) ? DefaultAlreadyAboard : settings.AlreadyAboard.Trim();
        settings.AlreadyAboardEventOpen = string.IsNullOrWhiteSpace(settings.AlreadyAboardEventOpen) ? DefaultAlreadyAboardEventOpen : settings.AlreadyAboardEventOpen.Trim();
        settings.AlreadyVotedEventOpen = string.IsNullOrWhiteSpace(settings.AlreadyVotedEventOpen) ? DefaultAlreadyVotedEventOpen : settings.AlreadyVotedEventOpen.Trim();

        settings.VoteRecorded = string.IsNullOrWhiteSpace(settings.VoteRecorded) ? DefaultVoteRecorded : settings.VoteRecorded.Trim();
        settings.VotePrompt = string.IsNullOrWhiteSpace(settings.VotePrompt) ? DefaultVotePrompt : settings.VotePrompt.Trim();
        settings.ConfirmNotNeeded = string.IsNullOrWhiteSpace(settings.ConfirmNotNeeded) ? DefaultConfirmNotNeeded : settings.ConfirmNotNeeded.Trim();
        settings.ConfirmSuccess = string.IsNullOrWhiteSpace(settings.ConfirmSuccess) ? DefaultConfirmSuccess : settings.ConfirmSuccess.Trim();

        settings.StartNotAuthorized = string.IsNullOrWhiteSpace(settings.StartNotAuthorized) ? DefaultStartNotAuthorized : settings.StartNotAuthorized.Trim();
        settings.StartSuccess = string.IsNullOrWhiteSpace(settings.StartSuccess) ? DefaultStartSuccess : settings.StartSuccess.Trim();
        settings.StopNotAuthorized = string.IsNullOrWhiteSpace(settings.StopNotAuthorized) ? DefaultStopNotAuthorized : settings.StopNotAuthorized.Trim();
        settings.StopNoneActive = string.IsNullOrWhiteSpace(settings.StopNoneActive) ? DefaultStopNoneActive : settings.StopNoneActive.Trim();
        settings.StopAborted = string.IsNullOrWhiteSpace(settings.StopAborted) ? DefaultStopAborted : settings.StopAborted.Trim();
        settings.AdvanceNotAuthorized = string.IsNullOrWhiteSpace(settings.AdvanceNotAuthorized) ? DefaultAdvanceNotAuthorized : settings.AdvanceNotAuthorized.Trim();

        settings.Help = string.IsNullOrWhiteSpace(settings.Help) ? DefaultHelp : settings.Help.Trim();

        settings.EventOpened = string.IsNullOrWhiteSpace(settings.EventOpened) ? DefaultEventOpened : settings.EventOpened.Trim();
        settings.EventStepAdvanced = string.IsNullOrWhiteSpace(settings.EventStepAdvanced) ? DefaultEventStepAdvanced : settings.EventStepAdvanced.Trim();
        settings.EventResolved = string.IsNullOrWhiteSpace(settings.EventResolved) ? DefaultEventResolved : settings.EventResolved.Trim();

        return settings;
    }

    public static string Format(string? template, string fallback, IReadOnlyDictionary<string, string> replacements)
    {
        var result = string.IsNullOrWhiteSpace(template) ? fallback : template;
        foreach (var (placeholder, value) in replacements)
        {
            result = result.Replace("{" + placeholder + "}", value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
}
