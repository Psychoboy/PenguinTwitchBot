using System.Collections;
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
        fields = EnhanceObsConnectionField(fields, subAction, scope.ServiceProvider);

        return subAction switch
        {
            ObsSetBrowserSourceUrlType browser => EnhanceObsBrowserSourceUrl(fields, browser, scope.ServiceProvider),
            ObsSetColorSourceColorType color => EnhanceObsColorSource(fields, color, scope.ServiceProvider),
            ObsSetImageSourceFileType image => EnhanceObsImageSource(fields, image, scope.ServiceProvider),
            ObsSetMediaSourceFileType mediaFile => EnhanceObsMediaSource(fields, mediaFile, scope.ServiceProvider),
            ObsSetMediaStateType mediaState => EnhanceObsMediaState(fields, mediaState, scope.ServiceProvider),
            ObsSetSceneType obs => EnhanceObsSetScene(fields, obs, scope.ServiceProvider),
            ObsSetSceneFilterStateType sceneFilter => EnhanceObsSceneFilterState(fields, sceneFilter, scope.ServiceProvider),
            ObsSetSourceAudioTrackStateType audioTrack => EnhanceObsAudioTrackState(fields, audioTrack, scope.ServiceProvider),
            ObsSetSourceFilterStateType sourceFilter => EnhanceObsSourceFilterState(fields, sourceFilter, scope.ServiceProvider),
            ObsSetSourceMuteStateType mute => EnhanceObsSourceMuteState(fields, mute, scope.ServiceProvider),
            ObsSetSourceVisibilityType visibility => EnhanceObsSourceVisibility(fields, visibility, scope.ServiceProvider),
            ObsSetTextType text => EnhanceObsTextSource(fields, text, scope.ServiceProvider),
            ObsTriggerHotkeyType hotkey => EnhanceObsHotkey(fields, hotkey, scope.ServiceProvider),
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

    private static List<SubActionUIField> EnhanceObsConnectionField(List<SubActionUIField> fields, SubActionType subAction, IServiceProvider serviceProvider)
    {
        var hasObsConnectionProperty = subAction.GetType().GetProperty(nameof(ObsSetSceneType.OBSConnectionId)) != null;
        if (!hasObsConnectionProperty)
            return fields;

        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null)
            return fields;

        var connections = Task.Run(async () => await connectionManager.GetAllConnectionsAsync()).GetAwaiter().GetResult();
        var connectionOptions = connections
            .Select(c => new SelectOption { Id = c.Id, Name = c.Name })
            .ToList();

        fields.RemoveAll(f => f.PropertyName == nameof(ObsSetSceneType.OBSConnectionId));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(ObsSetSceneType.OBSConnectionId),
            Label = "OBS Connection",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = connectionOptions
        });

        return fields;
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
                    var scenes = GetSceneNames(connected);
                    if (scenes.Count > 0)
                    {
                        var sceneField = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetSceneType.SceneName));
                        if (sceneField != null)
                        {
                            ApplySelectOptions(sceneField, scenes, "Select the OBS scene to switch to");
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

    private static List<SubActionUIField> EnhanceObsBrowserSourceUrl(List<SubActionUIField> fields, ObsSetBrowserSourceUrlType browserSource, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null || !browserSource.OBSConnectionId.HasValue)
            return fields;

        var connected = connectionManager.GetAllManagedConnections().FirstOrDefault(x => x.Id == browserSource.OBSConnectionId.Value && x.IsConnected);
        if (connected == null)
            return fields;

        var field = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetBrowserSourceUrlType.InputName));
        if (field == null)
            return fields;

        var inputNames = GetInputNames(connected, ["browser_source"]);
        ApplySelectOptions(field, inputNames, "Select the OBS browser source to update");
        return fields;
    }

    private static List<SubActionUIField> EnhanceObsColorSource(List<SubActionUIField> fields, ObsSetColorSourceColorType colorSource, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null || !colorSource.OBSConnectionId.HasValue)
            return fields;

        var connected = connectionManager.GetAllManagedConnections().FirstOrDefault(x => x.Id == colorSource.OBSConnectionId.Value && x.IsConnected);
        if (connected == null)
            return fields;

        var field = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetColorSourceColorType.InputName));
        if (field == null)
            return fields;

        var inputNames = GetInputNames(connected, ["color_source", "color_source_v2", "color_source_v3"]);
        ApplySelectOptions(field, inputNames, "Select the OBS color source");
        return fields;
    }

    private static List<SubActionUIField> EnhanceObsImageSource(List<SubActionUIField> fields, ObsSetImageSourceFileType imageSource, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null || !imageSource.OBSConnectionId.HasValue)
            return fields;

        var connected = connectionManager.GetAllManagedConnections().FirstOrDefault(x => x.Id == imageSource.OBSConnectionId.Value && x.IsConnected);
        if (connected == null)
            return fields;

        var field = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetImageSourceFileType.InputName));
        if (field == null)
            return fields;

        var inputNames = GetInputNames(connected, ["image_source"]);
        ApplySelectOptions(field, inputNames, "Select the OBS image source");
        return fields;
    }

    private static List<SubActionUIField> EnhanceObsMediaSource(List<SubActionUIField> fields, ObsSetMediaSourceFileType mediaSource, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null || !mediaSource.OBSConnectionId.HasValue)
            return fields;

        var connected = connectionManager.GetAllManagedConnections().FirstOrDefault(x => x.Id == mediaSource.OBSConnectionId.Value && x.IsConnected);
        if (connected == null)
            return fields;

        var field = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetMediaSourceFileType.InputName));
        if (field == null)
            return fields;

        var inputNames = GetInputNames(connected, ["ffmpeg_source", "vlc_source"]);
        ApplySelectOptions(field, inputNames, "Select the OBS media source");
        return fields;
    }

    private static List<SubActionUIField> EnhanceObsMediaState(List<SubActionUIField> fields, ObsSetMediaStateType mediaState, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null || !mediaState.OBSConnectionId.HasValue)
            return fields;

        var connected = connectionManager.GetAllManagedConnections().FirstOrDefault(x => x.Id == mediaState.OBSConnectionId.Value && x.IsConnected);
        if (connected == null)
            return fields;

        var field = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetMediaStateType.InputName));
        if (field == null)
            return fields;

        var inputNames = GetInputNames(connected, ["ffmpeg_source", "vlc_source"]);
        ApplySelectOptions(field, inputNames, "Select the OBS media source");
        return fields;
    }

    private static List<SubActionUIField> EnhanceObsSceneFilterState(List<SubActionUIField> fields, ObsSetSceneFilterStateType sceneFilter, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null || !sceneFilter.OBSConnectionId.HasValue)
            return fields;

        var connected = connectionManager.GetAllManagedConnections().FirstOrDefault(x => x.Id == sceneFilter.OBSConnectionId.Value && x.IsConnected);
        if (connected == null)
            return fields;

        var sceneField = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetSceneFilterStateType.SceneName));
        if (sceneField != null)
        {
            var scenes = GetSceneNames(connected);
            ApplySelectOptions(sceneField, scenes, "Select the OBS scene");
        }

        var filterField = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetSceneFilterStateType.FilterName));
        if (filterField != null && !string.IsNullOrWhiteSpace(sceneFilter.SceneName))
        {
            var filters = GetFilterNames(connected, sceneFilter.SceneName);
            ApplySelectOptions(filterField, filters, "Select the OBS filter");
            filterField.DependsOn = [nameof(ObsSetSceneFilterStateType.SceneName)];
        }

        return fields;
    }

    private static List<SubActionUIField> EnhanceObsSourceVisibility(List<SubActionUIField> fields, ObsSetSourceVisibilityType visibility, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null || !visibility.OBSConnectionId.HasValue)
            return fields;

        var connected = connectionManager.GetAllManagedConnections().FirstOrDefault(x => x.Id == visibility.OBSConnectionId.Value && x.IsConnected);
        if (connected == null)
            return fields;

        var sceneField = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetSourceVisibilityType.SceneName));
        if (sceneField != null)
        {
            var scenes = GetSceneNames(connected);
            ApplySelectOptions(sceneField, scenes, "Select the OBS scene");
        }

        var sourceField = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetSourceVisibilityType.SourceName));
        if (sourceField != null && !string.IsNullOrWhiteSpace(visibility.SceneName))
        {
            var sources = GetSceneItemNames(connected, visibility.SceneName);
            ApplySelectOptions(sourceField, sources, "Select the OBS source in the scene");
            sourceField.DependsOn = [nameof(ObsSetSourceVisibilityType.SceneName)];
        }

        return fields;
    }

    private static List<SubActionUIField> EnhanceObsSourceMuteState(List<SubActionUIField> fields, ObsSetSourceMuteStateType muteState, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null || !muteState.OBSConnectionId.HasValue)
            return fields;

        var connected = connectionManager.GetAllManagedConnections().FirstOrDefault(x => x.Id == muteState.OBSConnectionId.Value && x.IsConnected);
        if (connected == null)
            return fields;

        var field = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetSourceMuteStateType.InputName));
        if (field == null)
            return fields;

        var inputNames = GetInputNames(connected);
        ApplySelectOptions(field, inputNames, "Select the OBS input to mute or unmute");
        return fields;
    }

    private static List<SubActionUIField> EnhanceObsSourceFilterState(List<SubActionUIField> fields, ObsSetSourceFilterStateType sourceFilter, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null || !sourceFilter.OBSConnectionId.HasValue)
            return fields;

        var connected = connectionManager.GetAllManagedConnections().FirstOrDefault(x => x.Id == sourceFilter.OBSConnectionId.Value && x.IsConnected);
        if (connected == null)
            return fields;

        var sourceField = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetSourceFilterStateType.SourceName));
        if (sourceField != null)
        {
            var sources = GetInputNames(connected);
            ApplySelectOptions(sourceField, sources, "Select the OBS source");
        }

        var filterField = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetSourceFilterStateType.FilterName));
        if (filterField != null && !string.IsNullOrWhiteSpace(sourceFilter.SourceName))
        {
            var filters = GetFilterNames(connected, sourceFilter.SourceName);
            ApplySelectOptions(filterField, filters, "Select the OBS filter");
            filterField.DependsOn = [nameof(ObsSetSourceFilterStateType.SourceName)];
        }

        return fields;
    }

    private static List<SubActionUIField> EnhanceObsAudioTrackState(List<SubActionUIField> fields, ObsSetSourceAudioTrackStateType audioTrack, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null || !audioTrack.OBSConnectionId.HasValue)
            return fields;

        var connected = connectionManager.GetAllManagedConnections().FirstOrDefault(x => x.Id == audioTrack.OBSConnectionId.Value && x.IsConnected);
        if (connected == null)
            return fields;

        var field = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetSourceAudioTrackStateType.InputName));
        if (field == null)
            return fields;

        var inputNames = GetInputNames(connected);
        ApplySelectOptions(field, inputNames, "Select the OBS input");
        return fields;
    }

    private static List<SubActionUIField> EnhanceObsTextSource(List<SubActionUIField> fields, ObsSetTextType textSource, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null || !textSource.OBSConnectionId.HasValue)
            return fields;

        var connected = connectionManager.GetAllManagedConnections().FirstOrDefault(x => x.Id == textSource.OBSConnectionId.Value && x.IsConnected);
        if (connected == null)
            return fields;

        var field = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetTextType.InputName));
        if (field == null)
            return fields;

        var inputNames = GetInputNames(connected, ["text_gdiplus", "text_gdiplus_v2", "text_gdiplus_v3", "text_ft2_source", "text_ft2_source_v2"]);
        ApplySelectOptions(field, inputNames, "Select the OBS text source");
        return fields;
    }

    private static List<SubActionUIField> EnhanceObsHotkey(List<SubActionUIField> fields, ObsTriggerHotkeyType hotkey, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null)
            return fields;

        var field = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsTriggerHotkeyType.HotkeyName));
        if (field == null)
            return fields;

        field.FieldType = UIFieldType.Select;
        field.Required = true;
        field.HelperText = "Select the OBS hotkey to trigger";
        return fields;
    }

    private static void ApplySelectOptions(SubActionUIField? field, IEnumerable<string> options, string helperText)
    {
        if (field == null)
            return;

        field.FieldType = UIFieldType.Select;
        field.SelectOptions = options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(option => option, StringComparer.OrdinalIgnoreCase)
            .Select(option => new SelectOption { Name = option, Value = option })
            .ToList();
        field.HelperText = helperText;
    }

    private static List<string> GetSceneNames(PenguinTwitchBot.Bot.ObsConnector.ManagedOBSConnection connection)
    {
        var names = new List<string>();
        connection.Execute(obs =>
        {
            var result = InvokeObsMethod(obs, "GetSceneList");
            names.AddRange(ExtractNames(result));
        });
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> GetInputNames(PenguinTwitchBot.Bot.ObsConnector.ManagedOBSConnection connection, IEnumerable<string>? allowedKinds = null)
    {
        var names = new List<string>();
        connection.Execute(obs =>
        {
            var result = InvokeObsMethod(obs, "GetInputList");
            names.AddRange(ExtractNames(result, allowedKinds));
        });
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> GetSceneItemNames(PenguinTwitchBot.Bot.ObsConnector.ManagedOBSConnection connection, string sceneName)
    {
        var names = new List<string>();
        connection.Execute(obs =>
        {
            var result = InvokeObsMethod(obs, "GetSceneItemList", sceneName);
            names.AddRange(ExtractNames(result));
        });
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> GetFilterNames(PenguinTwitchBot.Bot.ObsConnector.ManagedOBSConnection connection, string sourceName)
    {
        var names = new List<string>();
        connection.Execute(obs =>
        {
            var result = InvokeObsMethod(obs, "GetSourceFilterList", sourceName);
            names.AddRange(ExtractNames(result));
        });
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static object? InvokeObsMethod(object obs, string methodName, params object?[] args)
    {
        var methods = obs.GetType().GetMethods()
            .Where(candidate => candidate.Name == methodName)
            .OrderBy(candidate => candidate.GetParameters().Length)
            .ToList();

        if (methods.Count == 0)
            return null;

        var method = methods.FirstOrDefault(candidate => candidate.GetParameters().Length == (args?.Length ?? 0));
        if (method == null)
        {
            method = methods.FirstOrDefault(candidate => candidate.GetParameters().Length >= (args?.Length ?? 0));
        }

        if (method == null)
            return null;

        try
        {
            var parameters = method.GetParameters();
            var invokeArgs = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i < (args?.Length ?? 0) && args != null)
                {
                    invokeArgs[i] = args[i];
                }
                else if (parameters[i].HasDefaultValue)
                {
                    invokeArgs[i] = parameters[i].DefaultValue;
                }
                else
                {
                    invokeArgs[i] = parameters[i].ParameterType.IsValueType
                        ? Activator.CreateInstance(parameters[i].ParameterType)
                        : null;
                }
            }

            return method.Invoke(obs, invokeArgs);
        }
        catch
        {
            return null;
        }
    }

    private static List<string> ExtractNames(object? data, IEnumerable<string>? allowedKinds = null)
    {
        if (data == null)
            return [];

        if (data is string text)
            return [text];

        if (data is IEnumerable enumerable && data is not string)
        {
            var names = new List<string>();
            foreach (var item in enumerable)
            {
                names.AddRange(ExtractNames(item, allowedKinds));
            }
            return names;
        }

        var type = data.GetType();
        if (allowedKinds != null)
        {
            var inputKindProperty = type.GetProperty("InputKind");
            if (inputKindProperty != null)
            {
                var inputKind = inputKindProperty.GetValue(data)?.ToString();
                if (!string.IsNullOrWhiteSpace(inputKind) && !allowedKinds.Contains(inputKind, StringComparer.OrdinalIgnoreCase))
                {
                    return [];
                }
            }
        }

        foreach (var propertyName in new[] { "Name", "InputName", "SourceName", "FilterName", "SceneName", "DisplayName", "ItemName" })
        {
            var property = type.GetProperty(propertyName);
            if (property == null)
                continue;

            var value = property.GetValue(data)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                return [value];
        }

        foreach (var propertyName in new[] { "InputName", "SourceName", "Name" })
        {
            var property = type.GetProperty(propertyName);
            if (property == null)
                continue;

            var value = property.GetValue(data);
            if (value is IEnumerable nestedValue && value is not string)
            {
                var nestedNames = ExtractNames(nestedValue, allowedKinds);
                if (nestedNames.Count > 0)
                    return nestedNames;
            }
        }

        foreach (var propertyName in new[] { "Inputs", "Scenes", "Sources", "Filters", "SceneItems", "Items" })
        {
            var property = type.GetProperty(propertyName);
            if (property == null)
                continue;

            var nested = property.GetValue(data);
            if (nested is IEnumerable enumerableValue && nested is not string)
            {
                var names = ExtractNames(enumerableValue, allowedKinds);
                if (names.Count > 0)
                    return names;
            }
        }

        return [];
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
