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
        settings.JoinSuccess = settings.JoinSuccess == null ? DefaultJoinSuccess : settings.JoinSuccess.Trim();
        settings.JoinSuccessEventOpen = settings.JoinSuccessEventOpen == null ? DefaultJoinSuccessEventOpen : settings.JoinSuccessEventOpen.Trim();
        settings.AlreadyAboard = settings.AlreadyAboard == null ? DefaultAlreadyAboard : settings.AlreadyAboard.Trim();
        settings.AlreadyAboardEventOpen = settings.AlreadyAboardEventOpen == null ? DefaultAlreadyAboardEventOpen : settings.AlreadyAboardEventOpen.Trim();
        settings.AlreadyVotedEventOpen = settings.AlreadyVotedEventOpen == null ? DefaultAlreadyVotedEventOpen : settings.AlreadyVotedEventOpen.Trim();

        settings.VoteRecorded = settings.VoteRecorded == null ? DefaultVoteRecorded : settings.VoteRecorded.Trim();
        settings.VotePrompt = settings.VotePrompt == null ? DefaultVotePrompt : settings.VotePrompt.Trim();
        settings.ConfirmNotNeeded = settings.ConfirmNotNeeded == null ? DefaultConfirmNotNeeded : settings.ConfirmNotNeeded.Trim();
        settings.ConfirmSuccess = settings.ConfirmSuccess == null ? DefaultConfirmSuccess : settings.ConfirmSuccess.Trim();

        settings.StartNotAuthorized = settings.StartNotAuthorized == null ? DefaultStartNotAuthorized : settings.StartNotAuthorized.Trim();
        settings.StartSuccess = settings.StartSuccess == null ? DefaultStartSuccess : settings.StartSuccess.Trim();
        settings.StopNotAuthorized = settings.StopNotAuthorized == null ? DefaultStopNotAuthorized : settings.StopNotAuthorized.Trim();
        settings.StopNoneActive = settings.StopNoneActive == null ? DefaultStopNoneActive : settings.StopNoneActive.Trim();
        settings.StopAborted = settings.StopAborted == null ? DefaultStopAborted : settings.StopAborted.Trim();
        settings.AdvanceNotAuthorized = settings.AdvanceNotAuthorized == null ? DefaultAdvanceNotAuthorized : settings.AdvanceNotAuthorized.Trim();

        settings.Help = settings.Help == null ? DefaultHelp : settings.Help.Trim();

        settings.EventOpened = settings.EventOpened == null ? DefaultEventOpened : settings.EventOpened.Trim();
        settings.EventStepAdvanced = settings.EventStepAdvanced == null ? DefaultEventStepAdvanced : settings.EventStepAdvanced.Trim();
        settings.EventResolved = settings.EventResolved == null ? DefaultEventResolved : settings.EventResolved.Trim();

        return settings;
    }

    public static string Format(string? template, string fallback, IReadOnlyDictionary<string, string> replacements)
    {
        var result = template ?? fallback;
        if (string.IsNullOrWhiteSpace(result))
        {
            return string.Empty;
        }

        foreach (var (placeholder, value) in replacements)
        {
            result = result.Replace("{" + placeholder + "}", value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
}
