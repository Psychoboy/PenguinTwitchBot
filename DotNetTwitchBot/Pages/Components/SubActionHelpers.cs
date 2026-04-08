using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Pages.Components
{
    public static class SubActionHelpers
    {
        public static string GetSubActionDescription(SubActionType subAction)
        {
            return subAction switch
            {
                SendMessageType msg => $"Message: {(msg.Text?.Length > 50 ? msg.Text[..50] + "..." : msg.Text)}",
                AlertType alert => $"Alert: {alert.Text} ({alert.Duration}s)",
                PlaySoundType sound => $"Sound: {sound.File}",
                RandomIntType random => $"Random: {random.Min} to {random.Max}",
                WriteFileType write => $"Write to: {write.File}",
                ExternalApiType api => $"API: {api.HttpMethod} {api.Text}",
                CurrentTimeType => "Gets current time",
                FollowAgeType => "Gets follow age",
                UptimeType => "Gets stream uptime",
                WatchTimeType => "Gets watch time",
                MultiCounterType multi => $"{multi.Name} counter. Variable: %counter_{multi.Name}%",
                ChannelPointSetEnabledStateType cp => $"Channel Point: {cp.Text} {(cp.EnablePoint ? "Enabled" : "Disabled")}",
                ChannelPointSetPausedStateType cp => $"Channel Point: {cp.Text} {(cp.IsPaused ? "Paused" : "Unpaused")}",
                TtsType tts => $"TTS: {(tts.Text?.Length > 50 ? tts.Text[..50] + "..." : tts.Text)}",
                LogicIfElseType ifElse => $"If {ifElse.LeftValue} {ifElse.Operator} {ifElse.RightValue} (True: {ifElse.TrueSubActions.Count}, False: {ifElse.FalseSubActions.Count})",
                ExecuteActionType exec => $"Execute Action: {exec.ActionName}",
                BreakType => "Breaks from current Action",
                DelayType delay => $"Delay: {delay.Duration}ms",
                ExecuteDefaultCommandType execCmd => $"Execute Command: {execCmd.CommandName}",
                ObsSetSceneFilterStateType obsFilter => $"OBS Filter: {obsFilter.SceneName} - {obsFilter.FilterName} {(obsFilter.FilterEnabled ? "Enabled" : "Disabled")}",
                ObsSetSceneType obsScene => $"OBS Scene: {obsScene.SceneName}",
                ReplyToMessageType reply => $"Reply: {(reply.Text?.Length > 50 ? reply.Text[..50] + "..." : reply.Text)}",
                SetVariableType setVar => $"Set Variable: {setVar.Text} = {setVar.Value}",
                ToggleCommandDisabledType toggleCmd => $"Toggle Command: {toggleCmd.CommandName} {(toggleCmd.IsDisabled ? "Disabled" : "Enabled")}",
                TimerGroupSetEnabledStateType timerGroup => $"Timer Group: {timerGroup.TimerGroupName} {(timerGroup.IsEnabled ? "Enabled" : "Disabled")}",
                CheckPointsType checkPoints => $"Gets {checkPoints.PointTypeName} points for {checkPoints.TargetUser} Variables: %TargetPoints% and %TargetPointsFormatted%",
                _ => subAction.Text?.Length > 0 ? subAction.Text : "No description available"
            };
        }
    }
}
