using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions;

public static partial class SubActionUIFieldEnhancer
{
    public static async Task<List<SubActionUIField>> GetEnhancedFieldsAsync(SubActionType? subAction, IServiceProvider? serviceProvider)
    {
        if (subAction is not ISubActionUIProvider uiProvider)
            return new List<SubActionUIField>();

        var fields = uiProvider.GetUIFields(serviceProvider);

        if (serviceProvider == null)
            return fields;

        await using var scope = serviceProvider.CreateAsyncScope();
        fields = await EnhanceObsConnectionFieldAsync(fields, subAction, scope.ServiceProvider);

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
            RaffleStartType => await EnhanceRaffleStartAsync(fields, scope.ServiceProvider),
            ObsSetTextType text => EnhanceObsTextSource(fields, text, scope.ServiceProvider),
            ExecuteActionType execute => await EnhanceExecuteActionAsync(fields, execute, scope.ServiceProvider),
            FishingGiveItemToPlayerType fishingGiveItem => await EnhanceFishingGiveItemToPlayerAsync(fields, fishingGiveItem, scope.ServiceProvider),
            FishingModifyType fishingModify => await EnhanceFishingModifyAsync(fields, fishingModify, scope.ServiceProvider),
            FishingTournamentStartType => await EnhanceFishingTournamentStartAsync(fields, scope.ServiceProvider),
            FishingTournamentEndType => await EnhanceFishingTournamentEndAsync(fields, scope.ServiceProvider),
            FishingFrenzyType fishFrenzy => await EnhanceFishingFrenzyAsync(fields, fishFrenzy, scope.ServiceProvider),
            FishingQuickTournamentType fishQuickTourney => await EnhanceFishingQuickTournamentAsync(fields, fishQuickTourney, scope.ServiceProvider),
            TimerGroupSetEnabledStateType timer => await EnhanceTimerGroupSetEnabledStateAsync(fields, timer, scope.ServiceProvider),
            ToggleCommandDisabledType toggle => await EnhanceToggleCommandDisabledAsync(fields, toggle, scope.ServiceProvider),
            PointCommandType point => await EnhancePointCommandAsync(fields, point, scope.ServiceProvider),
            GiftPointsType gift => await EnhanceGiftPointsAsync(fields, gift, scope.ServiceProvider),
            ManagePointsType managePoints => await EnhanceManagePointsAsync(fields, managePoints, scope.ServiceProvider),
            ForEachViewerType foreachViewer => await EnhanceForEachViewerAsync(fields, foreachViewer, scope.ServiceProvider),
            ExecuteDefaultCommandType defaultCmd => await EnhanceExecuteDefaultCommandAsync(fields, defaultCmd, scope.ServiceProvider),
            ExecuteCommandType cmd => await EnhanceExecuteCommandAsync(fields, cmd, scope.ServiceProvider),
            ChannelPointSetEnabledStateType channelPoint => await EnhanceChannelPointSetEnabledStateAsync(fields, channelPoint, scope.ServiceProvider),
            CheckPointsType checkPoints => await EnhanceCheckPointsAsync(fields, checkPoints, scope.ServiceProvider),
            SetGlobalVariableType setGlobalVariable => await EnhanceGlobalVariableNamesAsync(fields, setGlobalVariable, scope.ServiceProvider),
            GetGlobalVariableType getGlobalVariable => await EnhanceGlobalVariableNamesAsync(fields, getGlobalVariable, scope.ServiceProvider),
            OpenAiType openAi => EnhanceOpenAiModels(fields, openAi, scope.ServiceProvider),
            OverlayTimerStartType or OverlayTimerStopType or OverlayTimerAddTimeType or OverlayTimerRemoveTimeType or OverlayTimerVisibilityType => EnhanceOverlayTimer(fields, scope.ServiceProvider),
            CreditsPlayType or CreditsStopType or CreditsPauseResumeType or CreditsMarkStartType => await EnhanceCreditsAsync(fields, scope.ServiceProvider),
            _ => fields
        };
    }
}
