using System.Collections;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions;

public static partial class SubActionUIFieldEnhancer
{
    private static readonly string[] TextInputKinds = ["text_gdiplus", "text_gdiplus_v2", "text_gdiplus_v3", "text_ft2_source", "text_ft2_source_v2"];
    private static readonly string[] BrowserInputKinds = ["browser_source"];

    private static async Task<List<SubActionUIField>> EnhanceObsConnectionFieldAsync(List<SubActionUIField> fields, SubActionType subAction, IServiceProvider serviceProvider)
    {
        var hasObsConnectionProperty = subAction.GetType().GetProperty(nameof(ObsSetSceneType.OBSConnectionId)) != null;
        if (!hasObsConnectionProperty)
            return fields;

        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null)
            return fields;

        var connections = await connectionManager.GetAllConnectionsAsync();
        var connectionOptions = connections
            .Select(c => new SelectOption { Id = c.Id, Name = c.Name })
            .ToList();

        var selectedConnectionId = subAction.GetType().GetProperty(nameof(ObsSetSceneType.OBSConnectionId))?.GetValue(subAction) as Guid?;
        if (selectedConnectionId.HasValue && selectedConnectionId.Value != Guid.Empty && connectionOptions.All(option => option.Id != selectedConnectionId.Value))
        {
            connectionOptions.Add(new SelectOption { Id = selectedConnectionId.Value, Name = $"Connection #{selectedConnectionId.Value}" });
        }

        fields.RemoveAll(f => f.PropertyName == nameof(ObsSetSceneType.OBSConnectionId));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(ObsSetSceneType.OBSConnectionId),
            Label = "OBS Connection",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = connectionOptions,
            HelperText = "Select your OBS connection"
        });

        return fields;
    }

    private static List<SubActionUIField> EnhanceObsSetScene(List<SubActionUIField> fields, ObsSetSceneType obs, IServiceProvider serviceProvider)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null)
            return fields;

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
                            ApplySelectOptions(sceneField, scenes, "Select the OBS scene to switch to", preserveOrder: true);
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
        => EnhanceObsInputField(fields, serviceProvider, browserSource.OBSConnectionId, nameof(ObsSetBrowserSourceUrlType.InputName), BrowserInputKinds, "Select the OBS browser source to update");

    private static List<SubActionUIField> EnhanceObsColorSource(List<SubActionUIField> fields, ObsSetColorSourceColorType colorSource, IServiceProvider serviceProvider)
        => EnhanceObsInputField(fields, serviceProvider, colorSource.OBSConnectionId, nameof(ObsSetColorSourceColorType.InputName), ["color_source", "color_source_v2", "color_source_v3"], "Select the OBS color source");

    private static List<SubActionUIField> EnhanceObsImageSource(List<SubActionUIField> fields, ObsSetImageSourceFileType imageSource, IServiceProvider serviceProvider)
        => EnhanceObsInputField(fields, serviceProvider, imageSource.OBSConnectionId, nameof(ObsSetImageSourceFileType.InputName), ["image_source"], "Select the OBS image source");

    private static List<SubActionUIField> EnhanceObsMediaSource(List<SubActionUIField> fields, ObsSetMediaSourceFileType mediaSource, IServiceProvider serviceProvider)
        => EnhanceObsInputField(fields, serviceProvider, mediaSource.OBSConnectionId, nameof(ObsSetMediaSourceFileType.InputName), ["ffmpeg_source", "vlc_source"], "Select the OBS media source");

    private static List<SubActionUIField> EnhanceObsMediaState(List<SubActionUIField> fields, ObsSetMediaStateType mediaState, IServiceProvider serviceProvider)
        => EnhanceObsInputField(fields, serviceProvider, mediaState.OBSConnectionId, nameof(ObsSetMediaStateType.InputName), ["ffmpeg_source", "vlc_source"], "Select the OBS media source");

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
            ApplySelectOptions(sceneField, scenes, "Select the OBS scene", preserveOrder: true);
        }

        var filterField = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetSceneFilterStateType.FilterName));
        if (filterField != null)
        {
            filterField.DependsOn = [nameof(ObsSetSceneFilterStateType.SceneName)];
        }

        if (filterField != null && !string.IsNullOrWhiteSpace(sceneFilter.SceneName))
        {
            var filters = GetFilterNames(connected, sceneFilter.SceneName);
            ApplySelectOptions(filterField, filters, "Select the OBS filter");
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
            ApplySelectOptions(sceneField, scenes, "Select the OBS scene", preserveOrder: true);
        }

        var sourceField = fields.FirstOrDefault(f => f.PropertyName == nameof(ObsSetSourceVisibilityType.SourceName));
        if (sourceField != null)
        {
            sourceField.DependsOn = [nameof(ObsSetSourceVisibilityType.SceneName)];
        }

        if (sourceField != null && !string.IsNullOrWhiteSpace(visibility.SceneName))
        {
            var sources = GetSceneItemNames(connected, visibility.SceneName);
            ApplySelectOptions(sourceField, sources, "Select the OBS source in the scene", preserveOrder: true);
            sourceField.AllowCustomValue = true;
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
        if (filterField != null)
        {
            filterField.DependsOn = [nameof(ObsSetSourceFilterStateType.SourceName)];
        }

        if (filterField != null && !string.IsNullOrWhiteSpace(sourceFilter.SourceName))
        {
            var filters = GetFilterNames(connected, sourceFilter.SourceName);
            ApplySelectOptions(filterField, filters, "Select the OBS filter");
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
        => EnhanceObsInputField(fields, serviceProvider, textSource.OBSConnectionId, nameof(ObsSetTextType.InputName), TextInputKinds, "Select the OBS text source");

    private static List<SubActionUIField> EnhanceObsInputField(List<SubActionUIField> fields, IServiceProvider serviceProvider, Guid? connectionId, string propertyName, IEnumerable<string>? allowedKinds, string helperText)
    {
        var connectionManager = serviceProvider.GetService<ObsConnector.IOBSConnectionManager>();
        if (connectionManager == null || !connectionId.HasValue)
            return fields;

        var connected = connectionManager.GetAllManagedConnections().FirstOrDefault(x => x.Id == connectionId.Value && x.IsConnected);
        if (connected == null)
            return fields;

        var field = fields.FirstOrDefault(f => f.PropertyName == propertyName);
        if (field == null)
            return fields;

        var inputNames = GetInputNames(connected, allowedKinds);
        ApplySelectOptions(field, inputNames, helperText);
        return fields;
    }

    private static void ApplySelectOptions(SubActionUIField? field, IEnumerable<string> options, string helperText, bool preserveOrder = false)
    {
        if (field == null)
            return;

        var selectOptions = options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(option => new SelectOption { Name = option, Value = option })
            .ToList();
        if (!preserveOrder)
            selectOptions = selectOptions.OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase).ToList();

        field.FieldType = UIFieldType.Select;
        field.SelectOptions = selectOptions;
        field.HelperText = helperText;
    }

    private static List<string> GetSceneNames(PenguinTwitchBot.Bot.ObsConnector.ManagedOBSConnection connection)
    {
        var names = new List<string>();
        connection.Execute(obs =>
        {
            var result = obs.GetSceneList();
            names.AddRange(ExtractNames(result));
        });
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Reverse()
            .ToList();
    }

    private static List<string> GetInputNames(PenguinTwitchBot.Bot.ObsConnector.ManagedOBSConnection connection, IEnumerable<string>? allowedKinds = null)
    {
        var names = new List<string>();
        connection.Execute(obs =>
        {
            var result = obs.GetInputList(null);
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
            var result = obs.GetSceneItemList(sceneName);
            names.AddRange(ExtractNames(result));
        });
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> GetFilterNames(PenguinTwitchBot.Bot.ObsConnector.ManagedOBSConnection connection, string sourceName)
    {
        var names = new List<string>();
        connection.Execute(obs =>
        {
            var result = obs.GetSourceFilterList(sourceName);
            names.AddRange(ExtractNames(result));
        });
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ExtractNames(object? data, IEnumerable<string>? allowedKinds = null)
    {
        if (data == null)
            return [];

        if (data is string text)
            return [text];

        if (data is IEnumerable enumerable && data is not string)
            return ExtractNamesFromEnumerable(enumerable, allowedKinds);

        return ExtractNamesFromObject(data, allowedKinds);
    }

    private static List<string> ExtractNamesFromEnumerable(IEnumerable enumerable, IEnumerable<string>? allowedKinds)
    {
        var names = new List<string>();
        foreach (var item in enumerable)
        {
            names.AddRange(ExtractNames(item, allowedKinds));
        }

        return names;
    }

    private static List<string> ExtractNamesFromObject(object data, IEnumerable<string>? allowedKinds)
    {
        var type = data.GetType();
        if (ShouldFilterOutByInputKind(data, type, allowedKinds))
            return [];

        var directName = TryGetFirstStringPropertyValue(data, type, ["Name", "InputName", "SourceName", "FilterName", "SceneName", "DisplayName", "ItemName"]);
        if (!string.IsNullOrWhiteSpace(directName))
            return [directName];

        var nestedNames = TryGetNestedNames(data, type, ["InputName", "SourceName", "Name"], allowedKinds);
        if (nestedNames.Count > 0)
            return nestedNames;

        return TryGetNestedNames(data, type, ["Inputs", "Scenes", "Sources", "Filters", "SceneItems", "Items"], allowedKinds);
    }

    private static bool ShouldFilterOutByInputKind(object data, Type type, IEnumerable<string>? allowedKinds)
    {
        if (allowedKinds == null)
            return false;

        var inputKindProperty = type.GetProperty("InputKind");
        if (inputKindProperty == null)
            return false;

        var inputKind = inputKindProperty.GetValue(data)?.ToString();
        return !string.IsNullOrWhiteSpace(inputKind) && !allowedKinds.Contains(inputKind, StringComparer.OrdinalIgnoreCase);
    }

    private static string? TryGetFirstStringPropertyValue(object data, Type type, IEnumerable<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = type.GetProperty(propertyName);
            if (property == null)
                continue;

            var value = property.GetValue(data)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static List<string> TryGetNestedNames(object data, Type type, IEnumerable<string> propertyNames, IEnumerable<string>? allowedKinds)
    {
        foreach (var propertyName in propertyNames)
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

        return [];
    }
}
