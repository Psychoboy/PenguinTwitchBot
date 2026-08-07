using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Timers;
using System.Text.Json;

namespace PenguinTwitchBot.Models.Actions;

public class ActionsImportExportPackage
{
    public string Version { get; set; } = "1.1";
    public DateTime ExportedAt { get; set; }
    public List<ActionImportExportDto> Actions { get; set; } = [];
}

public class ActionImportExportDto
{
    public string Name { get; set; } = "";
    public string? Group { get; set; }
    public bool Enabled { get; set; } = true;
    public bool RandomAction { get; set; }
    public bool ConcurrentAction { get; set; }
    public bool OnlineOnly { get; set; }
    public string? QueueName { get; set; }
    public List<SubActionType> SubActions { get; set; } = [];
    public List<SubActionType> CatchSubActions { get; set; } = [];
    public List<TriggerImportExportDto> Triggers { get; set; } = [];
}

public class TriggerImportExportDto
{
    public string Name { get; set; } = "";
    public TriggerTypes Type { get; set; }
    public string Configuration { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public ActionCommandImportExportDto? CommandDefinition { get; set; }
    public ActionKeywordImportExportDto? KeywordDefinition { get; set; }
    public TimerGroupImportExportDto? TimerGroupDefinition { get; set; }
    public DefaultCommandTriggerImportExportDto? DefaultCommandDefinition { get; set; }
}

public class ActionCommandImportExportDto
{
    public string CommandName { get; set; } = "";
    public int UserCooldown { get; set; }
    public int UserCooldownMax { get; set; }
    public int GlobalCooldown { get; set; }
    public int GlobalCooldownMax { get; set; }
    public Rank MinimumRank { get; set; } = Rank.Viewer;
    public int Cost { get; set; }
    public bool Disabled { get; set; }
    public bool SayCooldown { get; set; } = true;

    public static ActionCommandImportExportDto FromCommand(ActionCommand command)
    {
        return new ActionCommandImportExportDto
        {
            CommandName = command.CommandName,
            UserCooldown = command.UserCooldown,
            UserCooldownMax = command.UserCooldownMax,
            GlobalCooldown = command.GlobalCooldown,
            GlobalCooldownMax = command.GlobalCooldownMax,
            MinimumRank = command.MinimumRank,
            Cost = command.Cost,
            Disabled = command.Disabled,
            SayCooldown = command.SayCooldown
        };
    }

    public ActionCommand ToCommand(string? overrideName = null)
    {
        return new ActionCommand
        {
            CommandName = string.IsNullOrWhiteSpace(overrideName) ? CommandName : overrideName,
            UserCooldown = UserCooldown,
            UserCooldownMax = UserCooldownMax,
            GlobalCooldown = GlobalCooldown,
            GlobalCooldownMax = GlobalCooldownMax,
            MinimumRank = MinimumRank,
            Cost = Cost,
            Disabled = Disabled,
            SayCooldown = SayCooldown
        };
    }
}

public class ActionKeywordImportExportDto
{
    public string CommandName { get; set; } = "";
    public int UserCooldown { get; set; }
    public int UserCooldownMax { get; set; }
    public int GlobalCooldown { get; set; }
    public int GlobalCooldownMax { get; set; }
    public Rank MinimumRank { get; set; } = Rank.Viewer;
    public int Cost { get; set; }
    public bool Disabled { get; set; }
    public bool SayCooldown { get; set; } = true;
    public string Response { get; set; } = "";
    public bool IsRegex { get; set; }
    public bool IsCaseSensitive { get; set; }

    public static ActionKeywordImportExportDto FromKeyword(ActionKeyword keyword)
    {
        return new ActionKeywordImportExportDto
        {
            CommandName = keyword.CommandName,
            UserCooldown = keyword.UserCooldown,
            UserCooldownMax = keyword.UserCooldownMax,
            GlobalCooldown = keyword.GlobalCooldown,
            GlobalCooldownMax = keyword.GlobalCooldownMax,
            MinimumRank = keyword.MinimumRank,
            Cost = keyword.Cost,
            Disabled = keyword.Disabled,
            SayCooldown = keyword.SayCooldown,
            Response = keyword.Response,
            IsRegex = keyword.IsRegex,
            IsCaseSensitive = keyword.IsCaseSensitive
        };
    }

    public ActionKeyword ToKeyword(string? overrideName = null)
    {
        return new ActionKeyword
        {
            CommandName = string.IsNullOrWhiteSpace(overrideName) ? CommandName : overrideName,
            UserCooldown = UserCooldown,
            UserCooldownMax = UserCooldownMax,
            GlobalCooldown = GlobalCooldown,
            GlobalCooldownMax = GlobalCooldownMax,
            MinimumRank = MinimumRank,
            Cost = Cost,
            Disabled = Disabled,
            SayCooldown = SayCooldown,
            Response = Response,
            IsRegex = IsRegex,
            IsCaseSensitive = IsCaseSensitive
        };
    }
}

public class TimerGroupImportExportDto
{
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public bool Repeat { get; set; } = true;
    public bool OnlineOnly { get; set; } = true;
    public int IntervalMinimumSeconds { get; set; } = 300;
    public int IntervalMaximumSeconds { get; set; } = 900;
    public int MinimumMessages { get; set; } = 15;
    public bool Shuffle { get; set; } = true;

    public static TimerGroupImportExportDto FromTimerGroup(TimerGroup timerGroup)
    {
        return new TimerGroupImportExportDto
        {
            Name = timerGroup.Name,
            Active = timerGroup.Active,
            Repeat = timerGroup.Repeat,
            OnlineOnly = timerGroup.OnlineOnly,
            IntervalMinimumSeconds = timerGroup.IntervalMinimumSeconds,
            IntervalMaximumSeconds = timerGroup.IntervalMaximumSeconds,
            MinimumMessages = timerGroup.MinimumMessages,
            Shuffle = timerGroup.Shuffle
        };
    }

    public TimerGroup ToTimerGroup(string? overrideName = null)
    {
        return new TimerGroup
        {
            Name = string.IsNullOrWhiteSpace(overrideName) ? Name : overrideName,
            Active = Active,
            Repeat = Repeat,
            OnlineOnly = OnlineOnly,
            IntervalMinimumSeconds = IntervalMinimumSeconds,
            IntervalMaximumSeconds = IntervalMaximumSeconds,
            MinimumMessages = MinimumMessages,
            Shuffle = Shuffle
        };
    }
}

public class DefaultCommandTriggerImportExportDto
{
    public string DefaultCommandName { get; set; } = "";
    public string EventType { get; set; } = "";
}

public static class ActionImportExportParsingHelper
{
    public static string? ExtractStringValue(string configuration, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(configuration))
            return null;

        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(configuration);
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch
        {
        }

        return null;
    }

    public static string? NormalizeCommandName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return name.Trim().TrimStart('!');
    }

    public static DefaultCommandTriggerImportExportDto? GetDefaultCommandDefinition(TriggerImportExportDto trigger)
    {
        if (trigger.DefaultCommandDefinition != null)
            return trigger.DefaultCommandDefinition;

        if (string.IsNullOrWhiteSpace(trigger.Configuration))
            return null;

        try
        {
            var config = JsonSerializer.Deserialize<DefaultCommandTriggerConfiguration>(trigger.Configuration);
            if (config == null || string.IsNullOrWhiteSpace(config.DefaultCommandName))
                return null;

            return new DefaultCommandTriggerImportExportDto
            {
                DefaultCommandName = config.DefaultCommandName,
                EventType = config.EventType
            };
        }
        catch
        {
            return null;
        }
    }
}