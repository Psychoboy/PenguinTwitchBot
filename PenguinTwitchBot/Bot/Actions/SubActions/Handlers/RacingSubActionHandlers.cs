using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Racing;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class RacingStartSessionHandler(IRacingGameService racingGameService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RacingStartSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not RacingStartSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type RacingStartSession is not of RacingStartSessionType class");
            }

            var resolvedMode = VariableReplacer.ReplaceVariables(sessionSubAction.GameMode ?? "", variables).Trim();
            RacingGameMode? gameModeOverride = resolvedMode switch
            {
                var m when m.Equals("GrandPrix", StringComparison.OrdinalIgnoreCase) => RacingGameMode.GrandPrix,
                var m when m.Equals("Elimination", StringComparison.OrdinalIgnoreCase) => RacingGameMode.Elimination,
                var m when m.Equals("DestructionDerby", StringComparison.OrdinalIgnoreCase) || m.Equals("Derby", StringComparison.OrdinalIgnoreCase) => RacingGameMode.DestructionDerby,
                _ => null
            };

            var resolvedTrack = VariableReplacer.ReplaceVariables(sessionSubAction.TrackType ?? "", variables).Trim();
            RacingTrackType? trackTypeOverride = resolvedTrack switch
            {
                var t when t.Equals("Perimeter", StringComparison.OrdinalIgnoreCase) => RacingTrackType.Perimeter,
                var t when t.Equals("EdgeOval", StringComparison.OrdinalIgnoreCase) || t.Equals("Oval", StringComparison.OrdinalIgnoreCase) => RacingTrackType.EdgeOval,
                var t when t.Equals("Figure8", StringComparison.OrdinalIgnoreCase) => RacingTrackType.Figure8,
                var t when t.Equals("DragStrip", StringComparison.OrdinalIgnoreCase) || t.Equals("Drag", StringComparison.OrdinalIgnoreCase) => RacingTrackType.DragStrip,
                var t when t.Equals("DerbyBowl", StringComparison.OrdinalIgnoreCase) || t.Equals("Arena", StringComparison.OrdinalIgnoreCase) => RacingTrackType.DerbyBowl,
                _ => null
            };

            int? lapsOverride = null;
            var resolvedLaps = VariableReplacer.ReplaceVariables(sessionSubAction.TotalLaps ?? "", variables).Trim();
            if (int.TryParse(resolvedLaps, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLaps) && parsedLaps > 0)
            {
                lapsOverride = parsedLaps;
            }

            int? durationSeconds = null;
            var resolvedDuration = VariableReplacer.ReplaceVariables(sessionSubAction.DurationSeconds ?? "", variables).Trim();
            if (int.TryParse(resolvedDuration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDuration) && parsedDuration > 0)
            {
                durationSeconds = parsedDuration;
            }

            var resolvedTheme = VariableReplacer.ReplaceVariables(sessionSubAction.ThemeOverrideId ?? "", variables).Trim();
            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";

            context?.LogMessage(subActionIndex, $"Starting Racing lobby (Mode: {gameModeOverride?.ToString() ?? "default"}, Track: {trackTypeOverride?.ToString() ?? "default"}, Laps: {lapsOverride?.ToString() ?? "default"}, Duration: {durationSeconds?.ToString() ?? "default"}s)");
            await racingGameService.StartRaceLobbyAsync(durationSeconds, resolvedTheme, triggeredBy, gameModeOverride, trackTypeOverride, lapsOverride);
        }
    }

    public class RacingStopSessionHandler(IRacingGameService racingGameService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RacingStopSession;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not RacingStopSessionType sessionSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type RacingStopSession is not of RacingStopSessionType class");
            }

            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";
            context?.LogMessage(subActionIndex, "Stopping Racing session");
            if (sessionSubAction.ClearQueue)
            {
                await racingGameService.ClearSessionQueueAsync();
            }
            await racingGameService.StopSessionAsync(triggeredBy);
        }
    }

    public class RacingJoinHandler(IRacingGameService racingGameService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RacingJoin;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not RacingJoinType joinSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type RacingJoin is not of RacingJoinType class");
            }

            var resolvedUser = VariableReplacer.ReplaceVariables(joinSubAction.Username ?? "%User%", variables).Trim();
            if (string.IsNullOrWhiteSpace(resolvedUser) || resolvedUser.Equals("%User%", StringComparison.OrdinalIgnoreCase))
            {
                resolvedUser = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "Viewer";
            }

            var resolvedEmote = VariableReplacer.ReplaceVariables(joinSubAction.EmoteOrImage ?? "", variables).Trim();
            if (resolvedEmote.Equals("%Args%", StringComparison.OrdinalIgnoreCase))
            {
                resolvedEmote = "";
            }

            var resolvedTheme = VariableReplacer.ReplaceVariables(joinSubAction.ThemeOverrideId ?? "", variables).Trim();
            var resolvedMode = VariableReplacer.ReplaceVariables(joinSubAction.EntryMode ?? "Auto", variables).Trim();

            var eventArgs = CommandEventArgsConverter.FromDictionaryOrNull(variables) ?? new CommandEventArgs
            {
                Name = resolvedUser.ToLowerInvariant(),
                DisplayName = resolvedUser,
                Arg = resolvedEmote,
                FromOwnChannel = true
            };

            var userChanged = !string.Equals(eventArgs.DisplayName, resolvedUser, StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(eventArgs.Name, resolvedUser, StringComparison.OrdinalIgnoreCase);

            eventArgs.DisplayName = resolvedUser;
            eventArgs.Name = resolvedUser.ToLowerInvariant();
            if (userChanged)
            {
                eventArgs.UserId = string.Empty;
            }

            if (resolvedMode.Equals("Solo", StringComparison.OrdinalIgnoreCase))
            {
                context?.LogMessage(subActionIndex, $"Triggering Solo Time Trial for {resolvedUser}");
                await racingGameService.TriggerSoloTimeTrialAsync(eventArgs, resolvedEmote, resolvedTheme, allowDirectUrlOrPath: true);
            }
            else if (resolvedMode.Equals("Lobby", StringComparison.OrdinalIgnoreCase))
            {
                context?.LogMessage(subActionIndex, $"Joining Race Lobby for {resolvedUser}");
                await racingGameService.TriggerJoinRaceAsync(eventArgs, resolvedEmote, resolvedTheme, allowDirectUrlOrPath: true);
            }
            else
            {
                var currentSession = racingGameService.GetCurrentSession();
                if (currentSession is { IsActive: true })
                {
                    context?.LogMessage(subActionIndex, $"Joining active Race Lobby for {resolvedUser}");
                    await racingGameService.TriggerJoinRaceAsync(eventArgs, resolvedEmote, resolvedTheme, allowDirectUrlOrPath: true);
                }
                else
                {
                    context?.LogMessage(subActionIndex, $"Triggering Solo Time Trial for {resolvedUser}");
                    await racingGameService.TriggerSoloTimeTrialAsync(eventArgs, resolvedEmote, resolvedTheme, allowDirectUrlOrPath: true);
                }
            }
        }
    }
}

