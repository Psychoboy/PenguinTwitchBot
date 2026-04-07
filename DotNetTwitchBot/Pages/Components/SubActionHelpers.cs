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
                MultiCounterType multi => $"{multi.Name} counter",
                ChannelPointSetEnabledStateType cp => $"Channel Point: {cp.Text} {(cp.EnablePoint ? "Enabled" : "Disabled")}",
                ChannelPointSetPausedStateType cp => $"Channel Point: {cp.Text} {(cp.IsPaused ? "Paused" : "Unpaused")}",
                TtsType tts => $"TTS: {(tts.Text?.Length > 50 ? tts.Text[..50] + "..." : tts.Text)}",
                LogicIfElseType ifElse => $"If {ifElse.LeftValue} {ifElse.Operator} {ifElse.RightValue} (True: {ifElse.TrueSubActions.Count}, False: {ifElse.FalseSubActions.Count})",
                ExecuteActionType exec => $"Execute Action: {exec.ActionName}",
                _ => subAction.Text?.Length > 0 ? subAction.Text : "No description available"
            };
        }
    }
}
