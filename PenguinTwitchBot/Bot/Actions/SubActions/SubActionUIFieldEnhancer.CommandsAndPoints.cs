using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Actions.SubActions;

public static partial class SubActionUIFieldEnhancer
{
    private static async Task<List<SubActionUIField>> EnhanceExecuteActionAsync(List<SubActionUIField> fields, ExecuteActionType execute, IServiceProvider serviceProvider)
    {
        var actionService = serviceProvider.GetRequiredService<IActionManagementService>();
        var actions = await actionService.GetActionSummariesAsync();
        var actionOptions = actions
            .Where(a => a.Id != Guid.Empty)
            .Select(a => new SelectOption { Name = a.Name, Id = a.Id })
            .OrderBy(a => a.Name)
            .ToList();

        fields.RemoveAll(f => f.PropertyName == nameof(ExecuteActionType.ActionId));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(ExecuteActionType.ActionId),
            Label = "Action to Execute",
            FieldType = UIFieldType.Select,
            SelectOptions = actionOptions,
            Required = true,
            Clearable = true
        });

        fields.RemoveAll(f => f.PropertyName == nameof(ExecuteActionType.RankToExecuteAs));
        fields.Insert(3, new SubActionUIField
        {
            PropertyName = nameof(ExecuteActionType.RankToExecuteAs),
            Label = "Rank Level to Run At",
            FieldType = UIFieldType.Select,
            Options = [.. Enum.GetNames<Rank>()],
            HelperText = "If elevated rank is enabled, execute the action at the selected level."
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceTimerGroupSetEnabledStateAsync(List<SubActionUIField> fields, TimerGroupSetEnabledStateType timer, IServiceProvider serviceProvider)
    {
        var timerService = serviceProvider.GetRequiredService<IAutoTimers>();
        var timerGroups = await timerService.GetTimerGroupsAsync();
        var timerGroupOptions = timerGroups
            .Where(tg => tg.Id != Guid.Empty)
            .Select(tg => new SelectOption { Name = tg.Name, Id = tg.Id })
            .OrderBy(tg => tg.Name)
            .ToList();

        fields.RemoveAll(f => f.PropertyName == nameof(TimerGroupSetEnabledStateType.TimerGroupId));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(TimerGroupSetEnabledStateType.TimerGroupId),
            Label = "Timer Group",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = timerGroupOptions
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceToggleCommandDisabledAsync(List<SubActionUIField> fields, ToggleCommandDisabledType toggle, IServiceProvider serviceProvider)
    {
        var commandService = serviceProvider.GetRequiredService<IActionCommandService>();
        var commands = await commandService.GetAllAsync();
        var commandOptions = commands
            .Select(a => new SelectOption { Name = a.CommandName, Value = a.CommandName })
            .OrderBy(a => a.Name)
            .ToList();

        fields.RemoveAll(f => f.PropertyName == nameof(ToggleCommandDisabledType.CommandName));
        fields.Insert(0, new SubActionUIField
        {
            Label = "Command",
            PropertyName = nameof(ToggleCommandDisabledType.CommandName),
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = commandOptions
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhancePointCommandAsync(List<SubActionUIField> fields, PointCommandType point, IServiceProvider serviceProvider)
    {
        var commandService = serviceProvider.GetRequiredService<IPointsSystem>();
        var commands = await commandService.GetAllPointCommands();
        var pointNames = commands.Select(x => x.CommandName).ToArray();

        fields.RemoveAll(f => f.PropertyName == "Text");
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = "Text",
            Label = "Point Command Name",
            FieldType = UIFieldType.Select,
            Required = true,
            Options = pointNames
        });

        var rankNames = Enum.GetNames<Rank>();
        fields.RemoveAll(f => f.PropertyName == nameof(PointCommandType.RankToExecuteAs));
        fields.Insert(1, new SubActionUIField
        {
            PropertyName = nameof(PointCommandType.RankToExecuteAs),
            Label = "Rank Level to Run At",
            FieldType = UIFieldType.Select,
            Options = [.. rankNames],
            HelperText = "If elevated rank is enabled, execute the command at the selected level."
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceGiftPointsAsync(List<SubActionUIField> fields, GiftPointsType gift, IServiceProvider serviceProvider)
    {
        var commandService = serviceProvider.GetRequiredService<IPointsSystem>();
        var pointTypes = await commandService.GetPointTypes();
        var pointNames = pointTypes.Select(pt => pt.Name).ToArray();

        fields.RemoveAll(f => f.PropertyName == "Text");
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = "Text",
            Label = "Point Name",
            FieldType = UIFieldType.Select,
            Required = true,
            Options = pointNames,
            HelperText = "The type of points to gift."
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceManagePointsAsync(List<SubActionUIField> fields, ManagePointsType managePoints, IServiceProvider serviceProvider)
    {
        var pointsSystem = serviceProvider.GetRequiredService<IPointsSystem>();
        var pointTypes = await pointsSystem.GetPointTypes();

        fields.RemoveAll(field => field.PropertyName == nameof(ManagePointsType.PointTypeName));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(ManagePointsType.PointTypeName),
            Label = "Point Type",
            FieldType = UIFieldType.Select,
            Required = true,
            Options = pointTypes.Select(pointType => pointType.Name).OrderBy(name => name).ToArray()
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceForEachViewerAsync(List<SubActionUIField> fields, ForEachViewerType foreachViewer, IServiceProvider serviceProvider)
    {
        var actionService = serviceProvider.GetRequiredService<IActionManagementService>();
        var actions = await actionService.GetActionSummariesAsync();
        var actionOptions = actions
            .Where(a => a.Id != Guid.Empty)
            .Select(a => new SelectOption { Name = a.Name, Id = a.Id })
            .OrderBy(a => a.Name)
            .ToList();

        fields.RemoveAll(f => f.PropertyName == nameof(ForEachViewerType.ActionId));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(ForEachViewerType.ActionId),
            Label = "Action to Run",
            FieldType = UIFieldType.Select,
            SelectOptions = actionOptions,
            Required = true,
            Clearable = true,
            HelperText = "The action to run for each viewer. The %user% variable will be set to each viewer's username."
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceExecuteDefaultCommandAsync(List<SubActionUIField> fields, ExecuteDefaultCommandType defaultCmd, IServiceProvider serviceProvider)
    {
        var commandHandler = serviceProvider.GetRequiredService<ICommandHandler>();
        var defaultCommands = await commandHandler.GetDefaultCommandsFromDb();
        var commands = defaultCommands.Select(a => new SelectOption { Name = a.CustomCommandName, Value = a.CommandName }).OrderBy(a => a.Name).ToList();

        fields.RemoveAll(f => f.PropertyName == nameof(ExecuteDefaultCommandType.CommandName));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(ExecuteDefaultCommandType.CommandName),
            Label = "Command",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = [.. commands]
        });

        fields.RemoveAll(f => f.PropertyName == nameof(ExecuteDefaultCommandType.RankToExecuteAs));
        fields.Insert(1, new SubActionUIField
        {
            PropertyName = nameof(ExecuteDefaultCommandType.RankToExecuteAs),
            Label = "Rank Level to Run At",
            FieldType = UIFieldType.Select,
            Options = [.. Enum.GetNames<Rank>()],
            HelperText = "If elevated rank is enabled, execute the command at the selected level."
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceExecuteCommandAsync(List<SubActionUIField> fields, ExecuteCommandType cmd, IServiceProvider serviceProvider)
    {
        var actionCommandService = serviceProvider.GetRequiredService<IActionCommandService>();
        var commandHandler = serviceProvider.GetRequiredService<ICommandHandler>();

        var actionCommands = await actionCommandService.GetAllAsync();
        var defaultCommands = await commandHandler.GetDefaultCommandsFromDb();

        var commands = new List<SelectOption>();
        foreach (var ac in actionCommands)
        {
            var cmdName = ac.CommandName.TrimStart('!');
            commands.Add(new SelectOption { Name = $"!{cmdName} (Action)", Value = cmdName });
        }
        foreach (var dc in defaultCommands)
        {
            var cmdName = dc.CustomCommandName.TrimStart('!');
            commands.Add(new SelectOption { Name = $"!{cmdName} (Default)", Value = cmdName });
        }

        commands = commands.DistinctBy(c => c.Value, StringComparer.OrdinalIgnoreCase).OrderBy(c => c.Name).ToList();

        fields.RemoveAll(f => f.PropertyName == nameof(ExecuteCommandType.CommandName));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(ExecuteCommandType.CommandName),
            Label = "Command",
            FieldType = UIFieldType.Select,
            Required = true,
            AllowCustomValue = true,
            SelectOptions = [.. commands],
            HelperText = "Select or type the command to execute."
        });

        fields.RemoveAll(f => f.PropertyName == nameof(ExecuteCommandType.RankToExecuteAs));
        fields.Insert(1, new SubActionUIField
        {
            PropertyName = nameof(ExecuteCommandType.RankToExecuteAs),
            Label = "Rank Level to Run At",
            FieldType = UIFieldType.Select,
            Options = [.. Enum.GetNames<Rank>()],
            HelperText = "If elevated rank is enabled, execute the command at the selected level."
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceChannelPointSetEnabledStateAsync(List<SubActionUIField> fields, ChannelPointSetEnabledStateType channelPoint, IServiceProvider serviceProvider)
    {
        var twitchService = serviceProvider.GetRequiredService<ITwitchService>();
        var channelPoints = await twitchService.GetChannelPointRewards();
        var names = channelPoints.Select(cp => cp.Title).ToList();

        fields.RemoveAll(f => f.PropertyName == "Text");
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = "Text",
            Label = "Reward Name",
            FieldType = UIFieldType.Select,
            Required = true,
            Options = [.. names],
            HelperText = "The name of the channel point reward to enable or disable"
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceRaffleStartAsync(List<SubActionUIField> fields, IServiceProvider serviceProvider)
    {
        var pointsSystem = serviceProvider.GetRequiredService<IPointsSystem>();
        var pointTypes = await pointsSystem.GetPointTypes();
        var pointTypeField = fields.FirstOrDefault(field => field.PropertyName == nameof(RaffleStartType.PointTypeId));
        if (pointTypeField is null)
            return fields;

        pointTypeField.FieldType = UIFieldType.Select;
        pointTypeField.Clearable = true;
        pointTypeField.SelectOptions = pointTypes
            .OrderBy(pointType => pointType.Name, StringComparer.OrdinalIgnoreCase)
            .Select(pointType => new SelectOption { Id = pointType.Id, Name = pointType.Name })
            .ToList();
        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceCheckPointsAsync(List<SubActionUIField> fields, CheckPointsType checkPoints, IServiceProvider serviceProvider)
    {
        var pointSystem = serviceProvider.GetRequiredService<IPointsSystem>();
        var pointTypes = await pointSystem.GetPointTypes();
        var pointTypeNames = pointTypes.Select(pt => pt.Name).ToArray();

        fields.RemoveAll(f => f.PropertyName == nameof(CheckPointsType.PointTypeName));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(CheckPointsType.PointTypeName),
            Label = "Point Type",
            FieldType = UIFieldType.Select,
            Required = true,
            Options = pointTypeNames
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceGlobalVariableNamesAsync(List<SubActionUIField> fields, SimpleSubActionType subAction, IServiceProvider serviceProvider)
    {
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        var globalVariables = await unitOfWork.GlobalVariables.GetAllOrderedAsync();
        var variableNames = globalVariables.Select(variable => variable.Name).ToArray();

        fields.RemoveAll(field => field.PropertyName == nameof(SubActionType.Text));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(SubActionType.Text),
            Label = "Global Variable Name",
            FieldType = UIFieldType.Select,
            Required = true,
            Clearable = true,
            AllowCustomValue = true,
            Options = variableNames,
            HelperText = subAction is SetGlobalVariableType
                ? "Type or select a name. New names will be created automatically when the subaction saves."
                : "Type or select the global variable name directly, without % signs."
        });

        return fields;
    }
}
