using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Database.Repository;

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
            FishingGiveItemToPlayerType fishingGiveItem => EnhanceFishingGiveItemToPlayer(fields, fishingGiveItem, scope.ServiceProvider),
            FishingTournamentStartType fishStart => EnhanceFishingTournamentStart(fields, scope.ServiceProvider),
            FishingTournamentEndType fishEnd => EnhanceFishingTournamentEnd(fields, scope.ServiceProvider),
            TimerGroupSetEnabledStateType timer => EnhanceTimerGroupSetEnabledState(fields, timer, scope.ServiceProvider),
            ToggleCommandDisabledType toggle => EnhanceToggleCommandDisabled(fields, toggle, scope.ServiceProvider),
            PointCommandType point => EnhancePointCommand(fields, point, scope.ServiceProvider),
            GiftPointsType gift => EnhanceGiftPoints(fields, gift, scope.ServiceProvider),
            ForEachViewerType foreachViewer => EnhanceForEachViewer(fields, foreachViewer, scope.ServiceProvider),
            ExecuteDefaultCommandType defaultCmd => EnhanceExecuteDefaultCommand(fields, defaultCmd, scope.ServiceProvider),
            ChannelPointSetEnabledStateType channelPoint => EnhanceChannelPointSetEnabledState(fields, channelPoint, scope.ServiceProvider),
            CheckPointsType checkPoints => EnhanceCheckPoints(fields, checkPoints, scope.ServiceProvider),
            SetGlobalVariableType setGlobalVariable => EnhanceGlobalVariableNames(fields, setGlobalVariable, scope.ServiceProvider),
            GetGlobalVariableType getGlobalVariable => EnhanceGlobalVariableNames(fields, getGlobalVariable, scope.ServiceProvider),
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
                catch (Exception)
                {
                    // Ignore OBS scene list errors - connection may be temporarily unavailable
                }
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

    private static List<SubActionUIField> EnhanceFishingGiveItemToPlayer(List<SubActionUIField> fields, FishingGiveItemToPlayerType giveItem, IServiceProvider serviceProvider)
    {
        var fishingService = serviceProvider.GetRequiredService<IFishingService>();
        var shopService = serviceProvider.GetRequiredService<IFishingShopService>();

        var players = Task.Run(async () => await fishingService.GetAllPlayersWithGold()).GetAwaiter().GetResult();
        var shopItems = Task.Run(async () => await shopService.GetAllShopItems()).GetAwaiter().GetResult();

        var playerOptions = players
            .Where(player => !string.IsNullOrWhiteSpace(player.Username))
            .Select(player => new SelectOption
            {
                Name = $"{player.Username} ({player.UserId})",
                Value = player.Username
            })
            .GroupBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(option => option.Name)
            .ToList();

        if (!string.IsNullOrWhiteSpace(giveItem.TargetName)
            && !playerOptions.Any(option => string.Equals(option.Value, giveItem.TargetName, StringComparison.OrdinalIgnoreCase)))
        {
            playerOptions.Add(new SelectOption { Name = giveItem.TargetName, Value = giveItem.TargetName });
        }

        var shopItemOptions = shopItems
            .Select(item => new SelectOption
            {
                Id = item.Id,
                Name = item.Enabled ? item.Name : $"{item.Name} (disabled)"
            })
            .OrderBy(option => option.Name)
            .ToList();

        if (giveItem.ShopItemId.HasValue
            && !shopItemOptions.Any(option => option.Id == giveItem.ShopItemId.Value))
        {
            var fallbackName = string.IsNullOrWhiteSpace(giveItem.ShopItemName)
                ? $"Item #{giveItem.ShopItemId.Value}"
                : giveItem.ShopItemName;
            shopItemOptions.Add(new SelectOption { Id = giveItem.ShopItemId.Value, Name = fallbackName });
        }

        fields.RemoveAll(field => field.PropertyName == nameof(FishingGiveItemToPlayerType.TargetName));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(FishingGiveItemToPlayerType.TargetName),
            Label = "Target Username",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = playerOptions,
            AllowCustomValue = true,
            HelperText = "Select or type the username of the player who should receive the fishing item."
        });

        fields.RemoveAll(field => field.PropertyName == nameof(FishingGiveItemToPlayerType.ShopItemId));
        fields.Insert(1, new SubActionUIField
        {
            PropertyName = nameof(FishingGiveItemToPlayerType.ShopItemId),
            Label = "Fishing Item",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = shopItemOptions,
            HelperText = "Select which fishing item to grant."
        });

        return fields;
    }

    private static List<SubActionUIField> EnhanceFishingTournamentStart(List<SubActionUIField> fields, IServiceProvider serviceProvider)
    {
        var fishingService = serviceProvider.GetRequiredService<IFishingService>();
        var tournaments = Task.Run(async () => await fishingService.GetAllFishingTournaments()).GetAwaiter().GetResult();
        var options = tournaments
            .Select(t => new SelectOption { Id = t.Id, Name = $"{t.Name} ({t.Status})" })
            .OrderBy(o => o.Name)
            .ToList();

        fields.RemoveAll(f => f.PropertyName == nameof(FishingTournamentStartType.TournamentId));
        fields.Add(new SubActionUIField
        {
            PropertyName = nameof(FishingTournamentStartType.TournamentId),
            Label = "Tournament / Template",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = options,
            HelperText = "Select an existing tournament to start, or a template to clone and start."
        });

        return fields;
    }

    private static List<SubActionUIField> EnhanceFishingTournamentEnd(List<SubActionUIField> fields, IServiceProvider serviceProvider)
    {
        var fishingService = serviceProvider.GetRequiredService<IFishingService>();
        var tournaments = Task.Run(async () => await fishingService.GetAllFishingTournaments()).GetAwaiter().GetResult();
        var options = tournaments
            .Select(t => new SelectOption { Id = t.Id, Name = $"{t.Name} ({t.Status})" })
            .OrderBy(o => o.Name)
            .ToList();

        fields.RemoveAll(f => f.PropertyName == nameof(FishingTournamentEndType.TournamentId));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(FishingTournamentEndType.TournamentId),
            Label = "Tournament",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = options,
            HelperText = "Fishing tournament to end."
        });

        return fields;
    }

    private static List<SubActionUIField> EnhanceTimerGroupSetEnabledState(List<SubActionUIField> fields, TimerGroupSetEnabledStateType timer, IServiceProvider serviceProvider)
    {
        var timerService = serviceProvider.GetRequiredService<IAutoTimers>();
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

    private static List<SubActionUIField> EnhanceGlobalVariableNames(List<SubActionUIField> fields, SimpleSubActionType subAction, IServiceProvider serviceProvider)
    {
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        var globalVariables = Task.Run(async () => await unitOfWork.GlobalVariables.GetAllOrderedAsync()).GetAwaiter().GetResult();
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
