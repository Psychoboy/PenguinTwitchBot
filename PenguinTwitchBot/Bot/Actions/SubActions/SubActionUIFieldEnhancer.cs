using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions;

public static class SubActionUIFieldEnhancer
{
    public static List<SubActionUIField> GetEnhancedFields(SubActionType? subAction, IServiceProvider? serviceProvider)
    {
        if (subAction is not ISubActionUIProvider uiProvider)
            return new List<SubActionUIField>();

        var fields = uiProvider.GetUIFields(serviceProvider);

        if (serviceProvider == null)
            return fields;

        using var scope = serviceProvider.CreateScope();

        return subAction switch
        {
            ObsSetSceneType obs => EnhanceObsSetScene(fields, obs, scope.ServiceProvider),
            ExecuteActionType execute => EnhanceExecuteAction(fields, execute, scope.ServiceProvider),
            TimerGroupSetEnabledStateType timer => EnhanceTimerGroupSetEnabledState(fields, timer, scope.ServiceProvider),
            ToggleCommandDisabledType toggle => EnhanceToggleCommandDisabled(fields, toggle, scope.ServiceProvider),
            PointCommandType point => EnhancePointCommand(fields, point, scope.ServiceProvider),
            GiftPointsType gift => EnhanceGiftPoints(fields, gift, scope.ServiceProvider),
            ForEachViewerType foreachViewer => EnhanceForEachViewer(fields, foreachViewer, scope.ServiceProvider),
            ExecuteDefaultCommandType defaultCmd => EnhanceExecuteDefaultCommand(fields, defaultCmd, scope.ServiceProvider),
            ChannelPointSetEnabledStateType channelPoint => EnhanceChannelPointSetEnabledState(fields, channelPoint, scope.ServiceProvider),
            CheckPointsType checkPoints => EnhanceCheckPoints(fields, checkPoints, scope.ServiceProvider),
            _ => fields
        };
    }

    private static List<SubActionUIField> EnhanceObsSetScene(List<SubActionUIField> fields, ObsSetSceneType obs, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null)
            return fields;

        var connections = Task.Run(async () => await connectionManager.GetAllConnectionsAsync()).GetAwaiter().GetResult();
        var connectionOptions = connections.Select(c => new SelectOption { Id = c.Id, Name = c.Name }).ToList();

        fields.RemoveAll(f => f.PropertyName == nameof(ObsSetSceneType.OBSConnectionId));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(ObsSetSceneType.OBSConnectionId),
            Label = "OBS Connection",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = connectionOptions
        });

        if (obs.OBSConnectionId.HasValue)
        {
            var managedConnections = connectionManager.GetAllManagedConnections();
            var connected = managedConnections.FirstOrDefault(x => x.Id == obs.OBSConnectionId.Value && x.IsConnected);
            if (connected != null)
            {
                try
                {
                    List<string>? scenes = null;
                    connected.Execute(o =>
                    {
                        var sceneList = o.GetSceneList();
                        scenes = sceneList.Scenes.Select(s => s.Name).Order().ToList();
                    });
                    if (scenes != null && scenes.Count > 0)
                    {
                        var sceneField = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetSceneType.SceneName));
                        if (sceneField != null)
                        {
                            sceneField.FieldType = UIFieldType.Select;
                            sceneField.Options = [.. scenes];
                            sceneField.HelperText = "Select the OBS scene to switch to";
                        }
                    }
                }
                catch { }
            }
        }

        return fields;
    }

    private static List<SubActionUIField> EnhanceExecuteAction(List<SubActionUIField> fields, ExecuteActionType execute, IServiceProvider serviceProvider)
    {
        var actionService = serviceProvider.GetRequiredService<IActionManagementService>();
        var actions = Task.Run(async () => await actionService.GetAllActionsAsync()).GetAwaiter().GetResult();
        var actionOptions = actions
            .Where(a => a.Id.HasValue)
            .Select(a => new SelectOption { Name = a.Name, Id = a.Id!.Value })
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

        return fields;
    }

    private static List<SubActionUIField> EnhanceTimerGroupSetEnabledState(List<SubActionUIField> fields, TimerGroupSetEnabledStateType timer, IServiceProvider serviceProvider)
    {
        var timerService = serviceProvider.GetRequiredService<AutoTimers>();
        var timerGroups = Task.Run(async () => await timerService.GetTimerGroupsAsync()).GetAwaiter().GetResult();
        var timerGroupOptions = timerGroups
            .Where(tg => tg.Id.HasValue)
            .Select(tg => new SelectOption { Name = tg.Name, Id = tg.Id!.Value })
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

    private static List<SubActionUIField> EnhanceToggleCommandDisabled(List<SubActionUIField> fields, ToggleCommandDisabledType toggle, IServiceProvider serviceProvider)
    {
        var commandService = serviceProvider.GetRequiredService<IActionCommandService>();
        var commands = Task.Run(async () => await commandService.GetAllAsync()).GetAwaiter().GetResult();
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

    private static List<SubActionUIField> EnhancePointCommand(List<SubActionUIField> fields, PointCommandType point, IServiceProvider serviceProvider)
    {
        var commandService = serviceProvider.GetRequiredService<IPointsSystem>();
        var commands = Task.Run(async () => await commandService.GetAllPointCommands()).GetAwaiter().GetResult();
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

    private static List<SubActionUIField> EnhanceGiftPoints(List<SubActionUIField> fields, GiftPointsType gift, IServiceProvider serviceProvider)
    {
        var commandService = serviceProvider.GetRequiredService<IPointsSystem>();
        var pointTypes = Task.Run(async () => await commandService.GetPointTypes()).GetAwaiter().GetResult();
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

    private static List<SubActionUIField> EnhanceForEachViewer(List<SubActionUIField> fields, ForEachViewerType foreachViewer, IServiceProvider serviceProvider)
    {
        var actionService = serviceProvider.GetRequiredService<IActionManagementService>();
        var actions = Task.Run(async () => await actionService.GetAllActionsAsync()).GetAwaiter().GetResult();
        var actionOptions = actions
            .Where(a => a.Id.HasValue)
            .Select(a => new SelectOption { Name = a.Name, Id = a.Id!.Value })
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

    private static List<SubActionUIField> EnhanceExecuteDefaultCommand(List<SubActionUIField> fields, ExecuteDefaultCommandType defaultCmd, IServiceProvider serviceProvider)
    {
        var commandHandler = serviceProvider.GetRequiredService<ICommandHandler>();
        var defaultCommands = Task.Run(async () => await commandHandler.GetDefaultCommandsFromDb()).GetAwaiter().GetResult();
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

    private static List<SubActionUIField> EnhanceChannelPointSetEnabledState(List<SubActionUIField> fields, ChannelPointSetEnabledStateType channelPoint, IServiceProvider serviceProvider)
    {
        var twitchService = serviceProvider.GetRequiredService<ITwitchService>();
        var channelPoints = Task.Run(async () => await twitchService.GetChannelPointRewards()).GetAwaiter().GetResult();
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

    private static List<SubActionUIField> EnhanceCheckPoints(List<SubActionUIField> fields, CheckPointsType checkPoints, IServiceProvider serviceProvider)
    {
        var pointSystem = serviceProvider.GetRequiredService<IPointsSystem>();
        var pointTypes = Task.Run(async () => await pointSystem.GetPointTypes()).GetAwaiter().GetResult();
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
}
