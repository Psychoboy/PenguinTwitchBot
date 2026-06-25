using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Commands;

namespace PenguinTwitchBot.Bot.Actions
{
    public interface IRaffleSetupService
    {
        Task<RaffleSetupResult> CreateAsync(RaffleSetupRequest request);
    }

    public sealed class RaffleSetupService(
        IUnitOfWork unitOfWork,
        IActionCommandService actionCommandService,
        IPointsSystem pointsSystem,
        ILogger<RaffleSetupService> logger) : IRaffleSetupService
    {
        public async Task<RaffleSetupResult> CreateAsync(RaffleSetupRequest request)
        {
            try
            {
                if (request is null)
                {
                    return RaffleSetupResult.Failure(["Request is required."]);
                }

                var normalized = NormalizeRequest(request);
                var errors = await ValidateAsync(normalized);
                if (errors.Count > 0)
                {
                    return RaffleSetupResult.Failure(errors);
                }

                var joinCommand = CreateCommand(
                    normalized.JoinCommandName,
                    $"Join the {normalized.RaffleName} raffle",
                    Rank.Viewer);

                var startCommand = CreateCommand(
                    normalized.StartCommandName,
                    $"Start the {normalized.RaffleName} raffle",
                    Rank.Moderator);

                var endCommand = CreateCommand(
                    normalized.EndCommandName,
                    $"End the {normalized.RaffleName} raffle",
                    Rank.Moderator);

                int? joinActionId;
                int? startActionId;
                int? endActionId;

                await using (var transaction = await unitOfWork.BeginTransactionAsync())
                {
                    await unitOfWork.ActionCommands.AddAsync(joinCommand);
                    await unitOfWork.ActionCommands.AddAsync(startCommand);
                    await unitOfWork.ActionCommands.AddAsync(endCommand);
                    await unitOfWork.SaveChangesAsync();

                    var joinAction = await unitOfWork.Actions.CreateActionAsync(CreateJoinAction(normalized, joinCommand));
                    var startAction = await unitOfWork.Actions.CreateActionAsync(CreateStartAction(normalized, startCommand));
                    var endAction = await unitOfWork.Actions.CreateActionAsync(CreateEndAction(normalized, endCommand));

                    joinActionId = joinAction.Id;
                    startActionId = startAction.Id;
                    endActionId = endAction.Id;

                    await transaction.CommitAsync();
                }

                try
                {
                    if (normalized.PointTypeId.HasValue)
                    {
                        await pointsSystem.SetPointTypeForGame(normalized.RaffleKey, normalized.PointTypeId.Value);
                    }
                    else
                    {
                        await pointsSystem.RegisterDefaultPointForGame(normalized.RaffleKey);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Raffle setup created but point mapping failed for {RaffleKey}", normalized.RaffleKey);
                }

                return RaffleSetupResult.SuccessResult(
                    normalized,
                    joinActionId,
                    startActionId,
                    endActionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating raffle setup");
                return RaffleSetupResult.Failure(["An error occurred while creating the raffle setup. Check logs for details."]);
            }
        }

        private async Task<List<string>> ValidateAsync(RaffleSetupRequest request)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.RaffleName))
            {
                errors.Add("Raffle name is required.");
            }

            if (string.IsNullOrWhiteSpace(request.RaffleKey))
            {
                errors.Add("Raffle key is required.");
            }

            if (string.IsNullOrWhiteSpace(request.JoinCommandName))
            {
                errors.Add("Join command is required.");
            }

            if (string.IsNullOrWhiteSpace(request.StartCommandName))
            {
                errors.Add("Start command is required.");
            }

            if (string.IsNullOrWhiteSpace(request.EndCommandName))
            {
                errors.Add("End command is required.");
            }

            if (request.WinnerCount < 1)
            {
                errors.Add("Winner count must be at least 1.");
            }

            if (request.TotalAward < 0)
            {
                errors.Add("Total award must be 0 or greater.");
            }

            var commandNames = new[] { request.JoinCommandName, request.StartCommandName, request.EndCommandName };
            if (commandNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != commandNames.Length)
            {
                errors.Add("Join, start, and end commands must all be different.");
            }

            foreach (var commandName in commandNames)
            {
                if (await actionCommandService.CommandExistsAsync(commandName))
                {
                    errors.Add($"Command '!{commandName}' already exists.");
                }
            }

            var actions = await unitOfWork.Actions.GetAllWithDetailsAsync();
            var existingKeys = GetConfiguredRaffleKeys(actions)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (existingKeys.Contains(request.RaffleKey))
            {
                errors.Add($"Raffle key '{request.RaffleKey}' is already used by another raffle setup.");
            }

            return errors;
        }

        private static RaffleSetupRequest NormalizeRequest(RaffleSetupRequest request)
        {
            return new RaffleSetupRequest
            {
                RaffleName = NormalizeText(request.RaffleName),
                RaffleKey = NormalizeText(request.RaffleKey),
                Group = NormalizeText(request.Group),
                QueueName = string.IsNullOrWhiteSpace(request.QueueName) ? "Default" : request.QueueName.Trim(),
                JoinCommandName = NormalizeCommandName(request.JoinCommandName),
                StartCommandName = NormalizeCommandName(request.StartCommandName),
                EndCommandName = NormalizeCommandName(request.EndCommandName),
                PointTypeId = request.PointTypeId,
                WinnerCount = Math.Max(1, request.WinnerCount),
                TotalAward = Math.Max(0, request.TotalAward),
                IncludeStarterMessages = request.IncludeStarterMessages
            };
        }

        private static string NormalizeCommandName(string? commandName) => NormalizeText(commandName).TrimStart('!');
        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;

        private static ActionCommand CreateCommand(string commandName, string description, Rank minimumRank)
        {
            return new ActionCommand
            {
                CommandName = commandName,
                Category = "Raffles",
                Description = description,
                Disabled = false,
                UserCooldown = 0,
                UserCooldownMax = 0,
                GlobalCooldown = 0,
                GlobalCooldownMax = 0,
                Cost = 0,
                MinimumRank = minimumRank,
                SayCooldown = true,
                SayRankRequirement = true,
                ExcludeFromUi = false,
                SourceOnly = true
            };
        }

        private static ActionType CreateStartAction(RaffleSetupRequest request, ActionCommand command)
        {
            var subActions = new List<SubActionType>
            {
                new RaffleStartType
                {
                    Index = 0,
                    RaffleKey = request.RaffleKey,
                    RaffleName = request.RaffleName,
                    JoinCommand = request.JoinCommandName,
                    PointGameName = request.RaffleKey,
                    WinnerCount = request.WinnerCount,
                    TotalAward = request.TotalAward.ToString()
                }
            };

            if (request.IncludeStarterMessages)
            {
                subActions.Add(new SendMessageType
                {
                    Index = 1,
                    Text = "%raffle_name% started. Type !%raffle_join_command% to enter. %raffle_winner_count% winner(s), %raffle_total_award% %raffle_point_game% total.",
                    UseBot = true,
                    FallBack = true,
                    StreamOnly = true
                });
            }

            return CreateAction($"Raffle: {request.RaffleName} Start", request, subActions, command);
        }

        private static ActionType CreateJoinAction(RaffleSetupRequest request, ActionCommand command)
        {
            var subActions = new List<SubActionType>
            {
                new RaffleEnterType
                {
                    Index = 0,
                    RaffleKey = request.RaffleKey
                }
            };

            if (request.IncludeStarterMessages)
            {
                subActions.Add(new LogicIfElseType
                {
                    Index = 1,
                    LeftValue = "%raffle_joined%",
                    RightValue = "true",
                    Operator = ComparisonOperator.Equals,
                    TrueSubActions =
                    [
                        new SendMessageType
                        {
                            Index = 0,
                            Text = "%raffle_username% joined %raffle_name%. Entries: %raffle_entry_count%.",
                            UseBot = true,
                            FallBack = true,
                            StreamOnly = true
                        }
                    ],
                    FalseSubActions =
                    [
                        new LogicIfElseType
                        {
                            Index = 0,
                            LeftValue = "%raffle_already_entered%",
                            RightValue = "true",
                            Operator = ComparisonOperator.Equals,
                            TrueSubActions =
                            [
                                new SendMessageType
                                {
                                    Index = 0,
                                    Text = "%raffle_username%, you are already entered in %raffle_name%.",
                                    UseBot = true,
                                    FallBack = true,
                                    StreamOnly = true
                                }
                            ],
                            FalseSubActions =
                            [
                                new SendMessageType
                                {
                                    Index = 0,
                                    Text = "%raffle_name% is not active right now.",
                                    UseBot = true,
                                    FallBack = true,
                                    StreamOnly = true
                                }
                            ]
                        }
                    ]
                });
            }

            return CreateAction($"Raffle: {request.RaffleName} Join", request, subActions, command);
        }

        private static ActionType CreateEndAction(RaffleSetupRequest request, ActionCommand command)
        {
            var subActions = new List<SubActionType>
            {
                new RaffleEndType
                {
                    Index = 0,
                    RaffleKey = request.RaffleKey
                }
            };

            if (request.IncludeStarterMessages)
            {
                subActions.Add(new LogicIfElseType
                {
                    Index = 1,
                    LeftValue = "%raffle_has_entries%",
                    RightValue = "true",
                    Operator = ComparisonOperator.Equals,
                    TrueSubActions =
                    [
                        new SendMessageType
                        {
                            Index = 0,
                            Text = "%raffle_name% ended. Winner(s): %raffle_winners%. %raffle_each_award% %raffle_point_game% each.",
                            UseBot = true,
                            FallBack = true,
                            StreamOnly = true
                        }
                    ],
                    FalseSubActions =
                    [
                        new SendMessageType
                        {
                            Index = 0,
                            Text = "%raffle_name% ended with no entries.",
                            UseBot = true,
                            FallBack = true,
                            StreamOnly = true
                        }
                    ]
                });
            }

            return CreateAction($"Raffle: {request.RaffleName} End", request, subActions, command);
        }

        private static ActionType CreateAction(string name, RaffleSetupRequest request, List<SubActionType> subActions, ActionCommand command)
        {
            return new ActionType
            {
                Name = name,
                Group = request.Group,
                Enabled = true,
                QueueName = request.QueueName,
                RandomAction = false,
                ConcurrentAction = false,
                OnlineOnly = false,
                SubActions = subActions,
                Triggers =
                [
                    new TriggerType
                    {
                        Name = $"!{command.CommandName}",
                        Type = TriggerTypes.Command,
                        Enabled = true,
                        CommandId = command.Id,
                        Configuration = JsonSerializer.Serialize(new
                        {
                            CommandId = command.Id,
                            CommandName = command.CommandName
                        })
                    }
                ]
            };
        }

        private static IEnumerable<string> GetConfiguredRaffleKeys(IEnumerable<ActionType> actions)
        {
            foreach (var action in actions)
            {
                foreach (var key in GetConfiguredRaffleKeys(action.SubActions))
                {
                    yield return key;
                }
            }
        }

        private static IEnumerable<string> GetConfiguredRaffleKeys(IEnumerable<SubActionType> subActions)
        {
            foreach (var subAction in subActions)
            {
                switch (subAction)
                {
                    case RaffleStartType start when !string.IsNullOrWhiteSpace(start.RaffleKey):
                        yield return start.RaffleKey.Trim();
                        break;
                    case RaffleEnterType enter when !string.IsNullOrWhiteSpace(enter.RaffleKey):
                        yield return enter.RaffleKey.Trim();
                        break;
                    case RaffleEndType end when !string.IsNullOrWhiteSpace(end.RaffleKey):
                        yield return end.RaffleKey.Trim();
                        break;
                    case RaffleSetWinnerCountType winnerCount when !string.IsNullOrWhiteSpace(winnerCount.RaffleKey):
                        yield return winnerCount.RaffleKey.Trim();
                        break;
                    case RaffleSetTotalAwardType totalAward when !string.IsNullOrWhiteSpace(totalAward.RaffleKey):
                        yield return totalAward.RaffleKey.Trim();
                        break;
                    case RaffleGetEntryCountType entryCount when !string.IsNullOrWhiteSpace(entryCount.RaffleKey):
                        yield return entryCount.RaffleKey.Trim();
                        break;
                    case LogicIfElseType logic:
                        foreach (var key in GetConfiguredRaffleKeys(logic.TrueSubActions))
                        {
                            yield return key;
                        }

                        foreach (var key in GetConfiguredRaffleKeys(logic.FalseSubActions))
                        {
                            yield return key;
                        }

                        break;
                }
            }
        }
    }

    public sealed class RaffleSetupRequest
    {
        public string RaffleName { get; set; } = string.Empty;
        public string RaffleKey { get; set; } = string.Empty;
        public string Group { get; set; } = "Raffles";
        public string QueueName { get; set; } = "Default";
        public string JoinCommandName { get; set; } = string.Empty;
        public string StartCommandName { get; set; } = string.Empty;
        public string EndCommandName { get; set; } = string.Empty;
        public int? PointTypeId { get; set; }
        public int WinnerCount { get; set; } = 1;
        public long TotalAward { get; set; }
        public bool IncludeStarterMessages { get; set; } = true;
    }

    public sealed class RaffleSetupResult
    {
        public bool Success { get; init; }
        public List<string> Errors { get; init; } = [];
        public int? JoinActionId { get; init; }
        public int? StartActionId { get; init; }
        public int? EndActionId { get; init; }
        public string RaffleKey { get; init; } = string.Empty;
        public string JoinCommandName { get; init; } = string.Empty;
        public string StartCommandName { get; init; } = string.Empty;
        public string EndCommandName { get; init; } = string.Empty;

        public static RaffleSetupResult Failure(List<string> errors) => new()
        {
            Success = false,
            Errors = errors
        };

        public static RaffleSetupResult SuccessResult(RaffleSetupRequest request, int? joinActionId, int? startActionId, int? endActionId) => new()
        {
            Success = true,
            JoinActionId = joinActionId,
            StartActionId = startActionId,
            EndActionId = endActionId,
            RaffleKey = request.RaffleKey,
            JoinCommandName = request.JoinCommandName,
            StartCommandName = request.StartCommandName,
            EndCommandName = request.EndCommandName
        };
    }
}
