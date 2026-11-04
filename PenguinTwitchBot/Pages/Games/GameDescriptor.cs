using MudBlazor;
using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Pages.Games;

public enum GameKind
{
    Overlay,
    Chat
}

public sealed record GameDescriptor(
    string Id,
    string Title,
    GameKind Kind,
    string Route,
    string DefaultCommandName,
    string FeatureKey,
    string Icon,
    Color Color,
    string Description,
    string? DisplayCommand = null,
    IReadOnlyList<string>? AdditionalCommandNames = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? OutcomeEventsByCommand = null,
    string? HelpTopic = null,
    string? TriggerGroupTitle = null)
{
    public IReadOnlyList<string> CommandNames =>
        [DefaultCommandName, .. AdditionalCommandNames ?? []];

    public IReadOnlyList<string> OutcomeEventTypes =>
        (OutcomeEventsByCommand?.Values ?? []).SelectMany(events => events).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public string CommandLabel => DisplayCommand ?? $"!{DefaultCommandName}";

    public string SettingsHref => Route;

    public IReadOnlyList<string> GetOutcomeEvents(string commandName) =>
        OutcomeEventsByCommand != null && OutcomeEventsByCommand.TryGetValue(commandName, out var events)
            ? events
            : [];
}

public static class GameDescriptorRegistry
{
    private static readonly IReadOnlyList<GameDescriptor> Games =
    [
        Create("parachute", "Parachute Drop", GameKind.Overlay, "/games/parachute", "drop", FeatureKeys.DropGames,
            Icons.Material.Filled.Paragliding, Color.Primary, "Drop through hazards and land for a prize."),
        Create("plinko", "Plinko Bounce", GameKind.Overlay, "/games/plinko", "plinko", FeatureKeys.DropGames,
            Icons.Material.Filled.Grain, Color.Secondary, "Drop a puck through the prize board."),
        Create("balloon", "Balloon Ascent", GameKind.Overlay, "/games/balloon", "balloon", FeatureKeys.DropGames,
            Icons.Material.Filled.Air, Color.Info, "Guide a balloon upward through hazards."),
        Create("curling", "Curling / Shuffleboard", GameKind.Overlay, "/games/curling", "curl", FeatureKeys.DropGames,
            Icons.Material.Filled.SportsHockey, Color.Primary, "Slide stones toward the target."),
        Create("golf", "Golf (Chip & Putt)", GameKind.Overlay, "/games/golf", "golf", FeatureKeys.DropGames,
            Icons.Material.Filled.GolfCourse, Color.Success, "Chip a shot onto the green."),
        Create("racing", "Racing", GameKind.Overlay, "/games/racing", "race", FeatureKeys.DropGames,
            Icons.Material.Filled.DirectionsCar, Color.Warning, "Race or enter a destruction derby.", additionalCommands: ["timetrial"], triggerGroupTitle: "Racing & Destruction Derby"),
        Create("starship", "Starship Expedition", GameKind.Overlay, "/games/starship", "starship", FeatureKeys.DropGames,
            Icons.Material.Filled.RocketLaunch, Color.Tertiary, "Take the crew on a branching space voyage.", displayCommand: "!expedition"),
        Create("ufoclaw", "UFO Claw", GameKind.Overlay, "/games/ufoclaw", "joinclaw", FeatureKeys.DropGames,
            Icons.Material.Filled.PrecisionManufacturing, Color.Info, "Configure the claw machine and prize queue.", displayCommand: "!claw", helpTopic: "ufoclaw"),
        Create("wheelspin", "Wheel Spin", GameKind.Overlay, "/wheelspin", "spinwheel", FeatureKeys.WheeledGame,
            Icons.Material.Filled.Casino, Color.Success, "Configure the prize wheel and spin automations.", helpTopic: "wheelspin"),
        Create("defuse", "Defuse", GameKind.Chat, "/games/chat-settings?game=defuse", "defuse", FeatureKeys.Defuse,
            Icons.Material.Filled.Warning, Color.Error, "Choose a wire and try to defuse the bomb.", helpTopic: "game-settings", triggerGroupTitle: "Bomb Defuse"),
        Create("heist", "Heist", GameKind.Chat, "/games/chat-settings?game=heist", "heist", FeatureKeys.Heist,
            Icons.Material.Filled.AccountBalance, Color.Warning, "Join a team heist and try to escape with the loot.", helpTopic: "game-settings", triggerGroupTitle: "Bank Heist"),
        Create("gamble", "Gamble", GameKind.Chat, "/games/chat-settings?game=gamble", "gamble", FeatureKeys.Gamble,
            Icons.Material.Filled.Casino, Color.Secondary, "Roll for points and build toward the jackpot.", helpTopic: "game-settings"),
        Create("slots", "Slots", GameKind.Chat, "/games/chat-settings?game=slots", "slot", FeatureKeys.Slots,
            Icons.Material.Filled.ViewWeek, Color.Info, "Spin the slots for a point payout.", helpTopic: "game-settings"),
        Create("roulette", "Roulette", GameKind.Chat, "/games/chat-settings?game=roulette", "roulette", FeatureKeys.Roulette,
            Icons.Material.Filled.Autorenew, Color.Error, "Risk points in a high-stakes roulette game.", helpTopic: "game-settings"),
        Create("ffa", "FFA", GameKind.Chat, "/games/chat-settings?game=ffa", "ffa", FeatureKeys.FFA,
            Icons.Material.Filled.EmojiEvents, Color.Success, "Join a free-for-all and try to be the last viewer standing.", helpTopic: "game-settings")
    ];

    public static IReadOnlyList<GameDescriptor> All => Games;

    public static GameDescriptor? FindById(string? id) =>
        Games.FirstOrDefault(game => string.Equals(game.Id, id, StringComparison.OrdinalIgnoreCase));

    public static GameDescriptor? FindByCommandName(string? commandName) =>
        Games.FirstOrDefault(game => game.CommandNames.Contains(commandName ?? string.Empty, StringComparer.OrdinalIgnoreCase));

    private static GameDescriptor Create(
        string id,
        string title,
        GameKind kind,
        string route,
        string commandName,
        string featureKey,
        string icon,
        Color color,
        string description,
        IReadOnlyList<string>? additionalCommands = null,
        string? displayCommand = null,
        string? helpTopic = null,
        string? triggerGroupTitle = null)
    {
        var commandNames = new[] { commandName }.Concat(additionalCommands ?? []).Distinct(StringComparer.OrdinalIgnoreCase);
        var outcomesByCommand = commandNames.ToDictionary(
            name => name,
            name => (IReadOnlyList<string>)TriggerCatalog.AllTriggers
                .Where(trigger => trigger.Type == TriggerTypes.DefaultCommand &&
                    string.Equals(trigger.DefaultCommandName, name, StringComparison.OrdinalIgnoreCase))
                .Select(trigger => trigger.DefaultCommandEventType)
                .Where(eventType => !string.IsNullOrWhiteSpace(eventType))
                .Select(eventType => eventType!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);

        return new GameDescriptor(id, title, kind, route, commandName, featureKey, icon, color, description,
            displayCommand, additionalCommands, outcomesByCommand, helpTopic, triggerGroupTitle);
    }
}