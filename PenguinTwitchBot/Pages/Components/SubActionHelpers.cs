using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Pages.Components
{
    public static class SubActionHelpers
    {
        public static string GetSubActionDescription(SubActionType subAction, SubActionReferenceNames? names = null)
        {
            string Name(Guid? id) => id is null || id == Guid.Empty ? "(none)" : names?.Resolve(id.Value) ?? id.Value.ToString();

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
                MultiCounterType multi => $"{multi.Name} counter ({multi.Operation}). Variable: %counter_{multi.Name}%",
                SelectRandomViewersType randomViewers => $"Select {randomViewers.ViewerCount} random {(randomViewers.ActiveOnly ? "active " : "")}viewer(s)",
                ChannelPointSetEnabledStateType cp => $"Channel Point: {cp.Text} {(cp.EnablePoint ? "Enabled" : "Disabled")}",
                ChannelPointSetPausedStateType cp => $"Channel Point: {cp.Text} {(cp.IsPaused ? "Paused" : "Unpaused")}",
                TtsType tts => $"TTS: {(tts.Text?.Length > 50 ? tts.Text[..50] + "..." : tts.Text)}",
                LogicIfElseType ifElse => $"If {ifElse.LeftValue} {ifElse.Operator} {ifElse.RightValue} (True: {ifElse.TrueSubActions.Count}, False: {ifElse.FalseSubActions.Count})",
                ExecuteActionType exec => string.IsNullOrWhiteSpace(exec.Text) ? $"Execute Action: {Name(exec.ActionId)}" : $"Execute Action: {Name(exec.ActionId)} ({exec.Text})",
                ForEachViewerType forEach => $"For Each Viewer: {Name(forEach.ActionId)}",
                BreakType => "Breaks from current Action",
                DelayType delay => $"Delay: {delay.Duration}ms",
                OverlayTimerStartType timerStart => $"Overlay Timer{(string.IsNullOrWhiteSpace(timerStart.TimerName) || timerStart.TimerName.Equals("Default", StringComparison.OrdinalIgnoreCase) ? "" : $" ({timerStart.TimerName})")}: start counting {timerStart.Direction}{(string.IsNullOrWhiteSpace(timerStart.StartTime) ? (timerStart.ResetOnStart ? " from zero" : "") : $" from {timerStart.StartTime}")}",
                OverlayTimerStopType timerStop => $"Overlay Timer{(string.IsNullOrWhiteSpace(timerStop.TimerName) || timerStop.TimerName.Equals("Default", StringComparison.OrdinalIgnoreCase) ? "" : $" ({timerStop.TimerName})")}: stop{(timerStop.ResetOnStop ? " and reset" : "")}",
                OverlayTimerAddTimeType timerAdd => $"Overlay Timer{(string.IsNullOrWhiteSpace(timerAdd.TimerName) || timerAdd.TimerName.Equals("Default", StringComparison.OrdinalIgnoreCase) ? "" : $" ({timerAdd.TimerName})")}: add {timerAdd.Amount}",
                OverlayTimerRemoveTimeType timerRemove => $"Overlay Timer{(string.IsNullOrWhiteSpace(timerRemove.TimerName) || timerRemove.TimerName.Equals("Default", StringComparison.OrdinalIgnoreCase) ? "" : $" ({timerRemove.TimerName})")}: remove {timerRemove.Amount}",
                ExecuteDefaultCommandType execCmd => $"Execute Default Command: {execCmd.CommandName}",
                ExecuteCommandType execCmd => string.IsNullOrWhiteSpace(execCmd.Text) ? $"Execute Command: {execCmd.CommandName}" : $"Execute Command: {execCmd.CommandName} ({execCmd.Text})",
                FishingTournamentStartType tournamentStart => $"Fishing Tournament Start: {Name(tournamentStart.TournamentId)}",
                FishingTournamentEndType tournamentEnd => $"Fishing Tournament End: {Name(tournamentEnd.TournamentId)}",
                FishingTournamentEligibleCatchType tournamentEligibleCatch => $"Fishing Tournament Eligible Catch {(tournamentEligibleCatch.Enabled ? "Enabled" : "Disabled")}",
                FishingModifyType modify => modify.TargetType == FishingModifyTargetType.Fish
                    ? $"Modify Fish: {(string.IsNullOrWhiteSpace(modify.TargetFish) ? "None" : modify.TargetFish)}"
                    : $"Modify Shop Item: {(string.IsNullOrWhiteSpace(modify.TargetShopItem) ? "None" : modify.TargetShopItem)}",
                ObsSetSceneFilterStateType obsFilter => $"OBS Filter: {obsFilter.SceneName} - {obsFilter.FilterName} {(obsFilter.FilterEnabled ? "Enabled" : "Disabled")}",
                ObsSetSceneType obsScene => $"OBS Scene: {obsScene.SceneName}",
                ObsSetSourceVisibilityType obsVis => $"OBS Visibility: {obsVis.SceneName}/{obsVis.SourceName} {(obsVis.Visible ? "Visible" : "Hidden")}",
                ObsSetSourceMuteStateType obsMute => $"OBS Mute: {obsMute.InputName} {(obsMute.Muted ? "Muted" : "Unmuted")}",
                ObsSetTextType obsText => $"OBS Text: {obsText.InputName} = {(obsText.TextContent?.Length > 30 ? obsText.TextContent[..30] + "..." : obsText.TextContent)}",
                ObsSetBrowserSourceUrlType obsUrl => $"OBS Browser URL: {obsUrl.InputName} = {(obsUrl.Url?.Length > 40 ? obsUrl.Url[..40] + "..." : obsUrl.Url)}",
                ObsSetColorSourceColorType obsColor => $"OBS Color: {obsColor.InputName} = {obsColor.Color}",
                ObsSetImageSourceFileType obsImg => $"OBS Image: {obsImg.InputName} = {System.IO.Path.GetFileName(obsImg.FilePath)}",
                ObsSetMediaSourceFileType obsMediaFile => $"OBS Media: {obsMediaFile.InputName} = {System.IO.Path.GetFileName(obsMediaFile.FilePath)}",
                ObsSetMediaStateType obsMediaState => $"OBS Media State: {obsMediaState.InputName} → {obsMediaState.MediaAction}",
                ObsSetSourceAudioTrackStateType obsTrack => $"OBS Audio Track: {obsTrack.InputName} Track {obsTrack.TrackNumber} {(obsTrack.TrackEnabled ? "Enabled" : "Disabled")}",
                ObsSetSourceFilterStateType obsSourceFilter => $"OBS Source Filter: {obsSourceFilter.SourceName} - {obsSourceFilter.FilterName} {(obsSourceFilter.FilterEnabled ? "Enabled" : "Disabled")}",
                ReplyToMessageType reply => $"Reply: {(reply.Text?.Length > 50 ? reply.Text[..50] + "..." : reply.Text)}",
                SetVariableType setVar => $"Set Variable: {setVar.Text} = {setVar.Value}",
                ToggleCommandDisabledType toggleCmd => $"Toggle Command: {toggleCmd.CommandName} {(toggleCmd.IsDisabled ? "Disabled" : "Enabled")}",
                TimerGroupSetEnabledStateType timerGroup => $"Timer Group: {Name(timerGroup.TimerGroupId)} {(timerGroup.IsEnabled ? "Enabled" : "Disabled")}",
                CheckPointsType checkPoints => $"Gets {checkPoints.PointTypeName} points for {checkPoints.TargetUser} Variables: %TargetPoints% and %TargetPointsFormatted%",
                OpenAiType ai => $"OpenAI: {(ai.Text?.Length > 50 ? ai.Text[..50] + "..." : ai.Text)} ({ai.Model})",
                ParachuteDropType pDrop => $"Parachute Drop: {pDrop.Username}{(string.IsNullOrWhiteSpace(pDrop.EmoteOrImage) ? "" : $" ({pDrop.EmoteOrImage})")}",
                ParachuteDropletsType droplets => $"Parachute Droplets: {droplets.Count}x {droplets.SizePreset} / {droplets.SpeedPreset} ({droplets.SpawnPattern})",
                ParachuteRocketsType rockets => $"Sky Rockets: {rockets.Count}x ({rockets.Pattern}{(string.IsNullOrWhiteSpace(rockets.TargetUser) ? "" : $" -> {rockets.TargetUser}")})",
                PlinkoDropType plinko => $"Plinko Bounce: {plinko.Username}{(string.IsNullOrWhiteSpace(plinko.EmoteOrImage) ? "" : $" ({plinko.EmoteOrImage})")}",
                BalloonAscentType balloon => $"Balloon Ascent: {balloon.Username}{(string.IsNullOrWhiteSpace(balloon.EmoteOrImage) ? "" : $" ({balloon.EmoteOrImage})")}",
                BalloonHazardsType bHazards => $"Balloon Hazards: {bHazards.Count}x {bHazards.SizePreset} / {bHazards.SpeedPreset} ({bHazards.SpawnPattern})",
                CurlingSlideType curling => $"Curling Slide: {curling.Username} (Power: {curling.Power}{(string.IsNullOrWhiteSpace(curling.EmoteOrImage) ? "" : $", {curling.EmoteOrImage}")})",
                GolfPuttType golf => $"Golf Shot: {golf.Username} (Power: {golf.Power}{(string.IsNullOrWhiteSpace(golf.EmoteOrImage) ? "" : $", {golf.EmoteOrImage}")})",
                StartDropGameSessionType startSession => $"Start {startSession.GameType} Session ({startSession.DurationSeconds}s)",
                StopDropGameSessionType stopSession => $"Stop {stopSession.GameType} Session",
                ParachuteStartSessionType pStart => $"Start Parachute Session ({(string.IsNullOrWhiteSpace(pStart.DurationSeconds) ? "default" : pStart.DurationSeconds + "s")})",
                ParachuteStopSessionType pStop => $"Stop Parachute Session{(pStop.ClearQueue ? " & clear queue" : "")}",
                PlinkoStartSessionType plStart => $"Start Plinko Session ({(string.IsNullOrWhiteSpace(plStart.DurationSeconds) ? "default" : plStart.DurationSeconds + "s")})",
                PlinkoStopSessionType plStop => $"Stop Plinko Session{(plStop.ClearQueue ? " & clear queue" : "")}",
                BalloonStartSessionType bStart => $"Start Balloon Session ({(string.IsNullOrWhiteSpace(bStart.DurationSeconds) ? "default" : bStart.DurationSeconds + "s")})",
                BalloonStopSessionType bStop => $"Stop Balloon Session{(bStop.ClearQueue ? " & clear queue" : "")}",
                CurlingStartSessionType cStart => $"Start Curling Session ({(string.IsNullOrWhiteSpace(cStart.DurationSeconds) ? "default" : cStart.DurationSeconds + "s")})",
                CurlingStopSessionType cStop => $"Stop Curling Session{(cStop.ClearQueue ? " & clear queue" : "")}",
                GolfStartSessionType gStart => $"Start Golf Session ({(string.IsNullOrWhiteSpace(gStart.DurationSeconds) ? "default" : gStart.DurationSeconds + "s")})",
                GolfStopSessionType gStop => $"Stop Golf Session{(gStop.ClearQueue ? " & clear queue" : "")}",
                RacingStartSessionType rStart => $"Start Race Lobby ({(string.IsNullOrWhiteSpace(rStart.GameMode) ? "Default Mode" : rStart.GameMode)}{(string.IsNullOrWhiteSpace(rStart.TrackType) ? "" : $", {rStart.TrackType}")}{(string.IsNullOrWhiteSpace(rStart.TotalLaps) ? "" : $", {rStart.TotalLaps} laps")})",
                RacingStopSessionType rStop => $"Stop Race Session{(rStop.ClearQueue ? " & clear queue" : "")}",
                RacingJoinType rJoin => $"Race Entry ({rJoin.EntryMode}): {rJoin.Username}{(string.IsNullOrWhiteSpace(rJoin.EmoteOrImage) ? "" : $" ({rJoin.EmoteOrImage})")}",
                StarshipShowStatusType starshipShow => $"Starship Status Widget: show for {starshipShow.DurationSeconds}s",
                StarshipShowEventType starshipShow => $"Starship Event Widget: show for {starshipShow.DurationSeconds}s",
                StarshipStartExpeditionType starshipStart => string.IsNullOrWhiteSpace(starshipStart.LegCount) ? "Start Starship Expedition using saved game settings" : $"Start Starship Expedition ({starshipStart.LegCount} legs)",
                StarshipStopExpeditionType => "Stop the active Starship Expedition",
                UfoClawControlType ufo => $"UFO Claw: {ufo.Operation}{(string.Equals(ufo.Operation, "show", StringComparison.OrdinalIgnoreCase) ? $" ({ufo.Mode})" : "")}",
                _ => subAction.Text?.Length > 0 ? subAction.Text : "No description available"
            };
        }
    }
}
