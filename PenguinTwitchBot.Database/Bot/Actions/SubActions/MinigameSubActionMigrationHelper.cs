using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions
{
    /// <summary>
    /// Converts legacy generic StartDropGameSessionType and single-game StopDropGameSessionType instances
    /// into their dedicated per-minigame SubActionType equivalents while preserving IDs, ordering, and settings.
    /// </summary>
    public static class MinigameSubActionMigrationHelper
    {
        public static SubActionType UpgradeLegacySubAction(SubActionType subAction)
        {
            if (subAction is LogicIfElseType logicIfElse)
            {
                UpgradeLegacySubActionsInList(logicIfElse.TrueSubActions);
                UpgradeLegacySubActionsInList(logicIfElse.FalseSubActions);
                return logicIfElse;
            }

            if (subAction is StartDropGameSessionType startSession)
            {
                var rawGame = (startSession.GameType ?? "Parachute").Trim();
                // Keep generic handler if GameType uses runtime variable replacement
                if (rawGame.Contains('%'))
                {
                    return startSession;
                }

                SubActionType upgraded = rawGame.ToLowerInvariant() switch
                {
                    "plinko" => new PlinkoStartSessionType
                    {
                        DurationSeconds = startSession.DurationSeconds,
                        ThemeOverrideId = startSession.ThemeOverrideId
                    },
                    "balloon" or "balloonascent" => new BalloonStartSessionType
                    {
                        DurationSeconds = startSession.DurationSeconds,
                        ThemeOverrideId = startSession.ThemeOverrideId
                    },
                    "curling" or "shuffleboard" => new CurlingStartSessionType
                    {
                        DurationSeconds = startSession.DurationSeconds,
                        ThemeOverrideId = startSession.ThemeOverrideId
                    },
                    "golf" or "chip" or "putt" => new GolfStartSessionType
                    {
                        DurationSeconds = startSession.DurationSeconds,
                        ThemeOverrideId = startSession.ThemeOverrideId
                    },
                    "racing" or "race" or "kart" => new RacingStartSessionType
                    {
                        DurationSeconds = startSession.DurationSeconds,
                        ThemeOverrideId = startSession.ThemeOverrideId
                    },
                    "racinggrandprix" or "grandprix" => new RacingStartSessionType
                    {
                        GameMode = "GrandPrix",
                        DurationSeconds = startSession.DurationSeconds,
                        ThemeOverrideId = startSession.ThemeOverrideId
                    },
                    "racingelimination" or "elimination" => new RacingStartSessionType
                    {
                        GameMode = "Elimination",
                        DurationSeconds = startSession.DurationSeconds,
                        ThemeOverrideId = startSession.ThemeOverrideId
                    },
                    "racingderby" or "destructionderby" or "derby" => new RacingStartSessionType
                    {
                        GameMode = "DestructionDerby",
                        TrackType = "DerbyBowl",
                        DurationSeconds = startSession.DurationSeconds,
                        ThemeOverrideId = startSession.ThemeOverrideId
                    },
                    _ => new ParachuteStartSessionType
                    {
                        DurationSeconds = startSession.DurationSeconds,
                        ThemeOverrideId = startSession.ThemeOverrideId
                    }
                };

                CopyBaseProperties(startSession, upgraded);
                return upgraded;
            }

            if (subAction is StopDropGameSessionType stopSession)
            {
                var rawGame = (stopSession.GameType ?? "All").Trim();
                if (rawGame.Contains('%') || rawGame.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    return stopSession;
                }

                SubActionType? upgraded = rawGame.ToLowerInvariant() switch
                {
                    "parachute" => new ParachuteStopSessionType { ClearQueue = false },
                    "plinko" => new PlinkoStopSessionType { ClearQueue = false },
                    "balloon" or "balloonascent" => new BalloonStopSessionType { ClearQueue = false },
                    "curling" or "shuffleboard" => new CurlingStopSessionType { ClearQueue = false },
                    "golf" or "chip" or "putt" => new GolfStopSessionType { ClearQueue = false },
                    "racing" or "race" or "kart" => new RacingStopSessionType { ClearQueue = false },
                    _ => null
                };

                if (upgraded != null)
                {
                    CopyBaseProperties(stopSession, upgraded);
                    return upgraded;
                }
            }

            return subAction;
        }

        public static void UpgradeLegacySubActionsInList(IList<SubActionType>? subActions)
        {
            if (subActions == null)
            {
                return;
            }

            for (int i = 0; i < subActions.Count; i++)
            {
                subActions[i] = UpgradeLegacySubAction(subActions[i]);
            }
        }

        private static void CopyBaseProperties(SubActionType source, SubActionType target)
        {
            target.Id = source.Id;
            target.ActionTypeId = source.ActionTypeId;
            target.CatchActionTypeId = source.CatchActionTypeId;
            target.Index = source.Index;
            target.Text = source.Text;
            target.Enabled = source.Enabled;
        }
    }
}

