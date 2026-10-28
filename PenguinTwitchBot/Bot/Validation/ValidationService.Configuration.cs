using System.Text.RegularExpressions;
using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.DatabaseTools;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Bot.Validation
{
    /// <summary>Cross-cutting checks that look at how actions, settings and integrations fit together.</summary>
    public partial class ValidationService
    {
        private const string IgnoredIssuesSettingName = "ValidationIgnoredIssues";

        public async Task<HashSet<string>> GetIgnoredIssueKeysAsync()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await db.Settings.GetAsync(x => x.Name == IgnoredIssuesSettingName)).FirstOrDefault();
            return ParseIgnoredKeys(setting?.StringSetting);
        }

        public async Task SetIssueIgnoredAsync(string key, bool ignored)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await db.Settings.GetAsync(x => x.Name == IgnoredIssuesSettingName)).FirstOrDefault();
            var keys = ParseIgnoredKeys(setting?.StringSetting);

            if (!(ignored ? keys.Add(key) : keys.Remove(key)))
            {
                return;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(keys.OrderBy(k => k, StringComparer.Ordinal));
            if (setting == null)
            {
                await db.Settings.AddAsync(new Database.Bot.Models.Setting
                {
                    Name = IgnoredIssuesSettingName,
                    DataType = Database.Bot.Models.Setting.DataTypeEnum.String,
                    StringSetting = json
                });
            }
            else
            {
                setting.StringSetting = json;
                db.Settings.Update(setting);
            }
            await db.SaveChangesAsync();

            lock (_lock)
            {
                foreach (var issue in _lastValidationResult?.Issues.Where(i => i.Key == key) ?? [])
                {
                    issue.Ignored = ignored;
                }
            }
        }

        private static HashSet<string> ParseIgnoredKeys(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            try
            {
                return new HashSet<string>(
                    System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [],
                    StringComparer.Ordinal);
            }
            catch (System.Text.Json.JsonException)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }
        }

        public async Task<ValidationResult> ValidateConfigurationAsync()
        {
            var result = new ValidationResult();

            await using var scope = _scopeFactory.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<IUnitOfWork>();
            var features = sp.GetRequiredService<IFeatureRuntimeCoordinator>();
            var queueManager = sp.GetRequiredService<IQueueManager>();
            var pointsSystem = sp.GetRequiredService<IPointsSystem>();
            var raidSettings = sp.GetRequiredService<IRaidRewardSettingsService>();

            var actions = await db.Actions.GetAllWithDetailsAsync();
            var queues = await queueManager.GetAllQueuesAsync();
            var obsConnections = await db.OBSConnections.GetAsync();
            var timerGroups = await db.TimerGroups.GetAsync();
            var actionCommands = await db.ActionCommands.GetAsync();
            var keywords = await db.ActionKeywords.GetAsync();
            var defaultCommands = await db.DefaultCommands.GetAllAsync();
            var pointTypes = (await pointsSystem.GetPointTypes()).ToList();

            result.Stats["Timer groups"] = timerGroups.Count;
            result.Stats["OBS connections"] = obsConnections.Count;
            result.Stats["Queues"] = queues.Count;
            result.Stats["Point types"] = pointTypes.Count;

            Run("queues", () => CheckQueues(actions, queues, result));
            Run("features", () => CheckFeatureGates(actions, features, result));
            Run("disabled targets", () => CheckDisabledTargets(actions, result));
            Run("obs", () => CheckObs(actions, obsConnections, result));
            Run("messages", () => CheckEmptyMessages(actions, result));
            Run("commands", () => CheckCommands(actions, actionCommands, defaultCommands, result));
            Run("keywords", () => CheckKeywords(keywords, result));
            Run("timers", () => CheckTimerGroups(actions, timerGroups, result));
            await RunAsync("point types", () => CheckPointTypeSettings(pointTypes, result, db));
            await RunAsync("raid rewards", () => CheckRaidRewards(raidSettings, pointTypes, result));

            _logger.LogDebug("Configuration validation found {IssueCount} issues", result.Issues.Count);
            return result;
        }

        // One failing check must not hide the results of the others.
        private void Run(string name, Action check)
        {
            try { check(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Validation check '{Check}' failed", name); }
        }

        private async Task RunAsync(string name, Func<Task> check)
        {
            try { await check(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Validation check '{Check}' failed", name); }
        }

        private static IEnumerable<SubActionType> Flatten(IEnumerable<SubActionType>? subActions)
        {
            if (subActions == null) yield break;
            foreach (var subAction in subActions)
            {
                yield return subAction;
                if (subAction is LogicIfElseType logic)
                {
                    foreach (var nested in Flatten(logic.TrueSubActions)) yield return nested;
                    foreach (var nested in Flatten(logic.FalseSubActions)) yield return nested;
                }
            }
        }

        private static IEnumerable<SubActionType> ActiveSubActions(Database.Bot.Actions.ActionType action) =>
            Flatten(action.SubActions).Concat(Flatten(action.CatchSubActions)).Where(s => s.Enabled);

        private static string DescribeSubAction(SubActionType subAction) =>
            SubActionRegistry.GetMetadata(subAction.SubActionTypes)?.DisplayName ?? subAction.GetType().Name.Replace("Type", "");

        private static ValidationIssue ForAction(
            Database.Bot.Actions.ActionType action,
            ValidationIssueType type,
            ValidationSeverity severity,
            string message,
            string? suggestion = null,
            string? link = null,
            string? detail = null) => new()
        {
            IssueType = type,
            Severity = severity,
            EntityType = "Action",
            EntityId = action.Id,
            EntityName = action.Name,
            Message = message,
            Suggestion = suggestion,
            Link = link,
            Detail = detail
        };

        private static void CheckQueues(
            List<Database.Bot.Actions.ActionType> actions,
            List<Database.Bot.Models.Queues.QueueConfiguration> queues,
            ValidationResult result)
        {
            var byName = queues.ToDictionary(q => q.Name, q => q, StringComparer.OrdinalIgnoreCase);

            foreach (var action in actions.Where(a => !string.IsNullOrWhiteSpace(a.QueueName)))
            {
                // "Default" is always available even without a saved configuration.
                if (action.QueueName.Equals("default", StringComparison.OrdinalIgnoreCase) && !byName.ContainsKey(action.QueueName))
                    continue;

                if (!byName.TryGetValue(action.QueueName, out var queue))
                {
                    result.Issues.Add(ForAction(action, ValidationIssueType.ActionQueueMissing, ValidationSeverity.Warning,
                        $"Action '{action.Name}' uses queue '{action.QueueName}', which is not configured.",
                        "Create that queue on the Queues page, or pick another queue for this action.",
                        "/actions/queues"));
                }
                else if (!queue.Enabled && action.Enabled)
                {
                    result.Issues.Add(ForAction(action, ValidationIssueType.ActionQueueDisabled, ValidationSeverity.Warning,
                        $"Action '{action.Name}' uses queue '{action.QueueName}', which is disabled.",
                        "Enable the queue on the Queues page, or move the action to another queue.",
                        "/actions/queues"));
                }
            }
        }

        private static void CheckFeatureGates(
            List<Database.Bot.Actions.ActionType> actions,
            IFeatureRuntimeCoordinator features,
            ValidationResult result)
        {
            var displayNames = features.GetFeatures().ToDictionary(f => f.Key, f => f.DisplayName, StringComparer.OrdinalIgnoreCase);

            foreach (var action in actions.Where(a => a.Enabled))
            {
                var blocked = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var trigger in action.Triggers.Where(t => t.Enabled))
                {
                    var key = TriggerFeatureGate.GetFeatureKey(trigger.Type);
                    if (key != null && !features.IsEnabled(key))
                        blocked[key] = blocked.GetValueOrDefault(key) + 1;
                }

                foreach (var subAction in ActiveSubActions(action))
                {
                    var key = SubActionFeatureGate.GetFeatureKey(subAction.SubActionTypes);
                    if (key != null && !SubActionFeatureGate.IsAvailable(subAction.SubActionTypes, features))
                        blocked[key] = blocked.GetValueOrDefault(key) + 1;
                }

                foreach (var (key, count) in blocked)
                {
                    var featureName = displayNames.GetValueOrDefault(key, key);
                    result.Issues.Add(ForAction(action, ValidationIssueType.ActionFeatureDisabled, ValidationSeverity.Warning,
                        $"Action '{action.Name}' has {count} trigger(s) or sub-action(s) that need the '{featureName}' feature, which is turned off, so they will not run.",
                        $"Turn '{featureName}' back on in Bot Settings, or remove those items from the action.",
                        "/settings/app", key));
                }
            }
        }

        private static void CheckDisabledTargets(List<Database.Bot.Actions.ActionType> actions, ValidationResult result)
        {
            var byId = actions.Where(a => a.Id != Guid.Empty).ToDictionary(a => a.Id);

            foreach (var action in actions.Where(a => a.Enabled))
            {
                var reported = new HashSet<Guid>();
                foreach (var subAction in ActiveSubActions(action))
                {
                    var targetId = subAction switch
                    {
                        ExecuteActionType execute => execute.ActionId,
                        ForEachViewerType forEach => forEach.ActionId,
                        _ => null
                    };

                    if (targetId is { } id && byId.TryGetValue(id, out var target) && !target.Enabled && reported.Add(id))
                    {
                        result.Issues.Add(ForAction(action, ValidationIssueType.ActionTargetDisabled, ValidationSeverity.Warning,
                            $"Action '{action.Name}' runs action '{target.Name}', which is disabled.",
                            $"Enable '{target.Name}', or remove the '{DescribeSubAction(subAction)}' sub-action.",
                            $"/actions/manage?actionId={action.Id}", id.ToString()));
                    }
                }
            }
        }

        private static void CheckObs(
            List<Database.Bot.Actions.ActionType> actions,
            List<Database.Bot.Models.Obs.OBSConnection> connections,
            ValidationResult result)
        {
            var byId = connections.ToDictionary(c => c.Id);

            foreach (var connection in connections.Where(c => c.Enabled && !c.IsConnected))
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.ObsConnectionFailing,
                    Severity = ValidationSeverity.Warning,
                    EntityType = "Integration",
                    EntityId = connection.Id,
                    EntityName = connection.Name,
                    Message = string.IsNullOrWhiteSpace(connection.LastError)
                        ? $"OBS connection '{connection.Name}' is enabled but not connected."
                        : $"OBS connection '{connection.Name}' is enabled but not connected: {connection.LastError}",
                    Suggestion = "Check that OBS is running, WebSocket server is enabled, and the URL and password are correct.",
                    Link = "/obs/connections"
                });
            }

            foreach (var action in actions.Where(a => a.Enabled))
            {
                var missingSelection = 0;
                var problems = new Dictionary<Guid, (string Reason, int Count)>();

                foreach (var subAction in ActiveSubActions(action))
                {
                    var property = subAction.GetType().GetProperty("OBSConnectionId");
                    if (property?.PropertyType != typeof(Guid?)) continue;

                    var connectionId = (Guid?)property.GetValue(subAction);
                    if (connectionId is null || connectionId == Guid.Empty)
                    {
                        missingSelection++;
                        continue;
                    }

                    if (!byId.TryGetValue(connectionId.Value, out var connection))
                    {
                        problems[connectionId.Value] = ("no longer exists", problems.GetValueOrDefault(connectionId.Value).Count + 1);
                    }
                    else if (!connection.Enabled)
                    {
                        problems[connectionId.Value] = ($"'{connection.Name}' is disabled", problems.GetValueOrDefault(connectionId.Value).Count + 1);
                    }
                }

                if (missingSelection > 0)
                {
                    result.Issues.Add(ForAction(action, ValidationIssueType.SubActionObsConnectionMissing, ValidationSeverity.Error,
                        $"Action '{action.Name}' has {missingSelection} OBS sub-action(s) with no OBS connection selected.",
                        "Edit those sub-actions and choose an OBS connection.",
                        $"/actions/manage?actionId={action.Id}"));
                }

                foreach (var (connectionId, (reason, count)) in problems)
                {
                    var missing = !byId.ContainsKey(connectionId);
                    result.Issues.Add(ForAction(action,
                        missing ? ValidationIssueType.SubActionObsConnectionMissing : ValidationIssueType.SubActionObsConnectionUnavailable,
                        missing ? ValidationSeverity.Error : ValidationSeverity.Warning,
                        $"Action '{action.Name}' has {count} OBS sub-action(s) whose OBS connection {reason}.",
                        missing ? "Edit those sub-actions and choose an existing OBS connection." : "Enable the connection on the OBS Connections page.",
                        missing ? $"/actions/manage?actionId={action.Id}" : "/obs/connections",
                        connectionId.ToString()));
                }
            }
        }

        private static void CheckEmptyMessages(List<Database.Bot.Actions.ActionType> actions, ValidationResult result)
        {
            foreach (var action in actions.Where(a => a.Enabled))
            {
                var empty = ActiveSubActions(action)
                    .Count(s => s is SendMessageType or ReplyToMessageType && string.IsNullOrWhiteSpace(s.Text));
                if (empty > 0)
                {
                    result.Issues.Add(ForAction(action, ValidationIssueType.SubActionEmptyMessage, ValidationSeverity.Warning,
                        $"Action '{action.Name}' has {empty} chat message sub-action(s) with no text, so nothing will be sent.",
                        "Add message text or disable those sub-actions.",
                        $"/actions/manage?actionId={action.Id}"));
                }
            }
        }

        private static void CheckCommands(
            List<Database.Bot.Actions.ActionType> actions,
            List<Database.Bot.Models.Commands.ActionCommand> commands,
            IEnumerable<Database.Bot.Models.Commands.DefaultCommand> defaultCommands,
            ValidationResult result)
        {
            var defaultNames = new HashSet<string>(defaultCommands.Select(d => d.CommandName), StringComparer.OrdinalIgnoreCase);

            foreach (var group in commands.Where(c => !string.IsNullOrWhiteSpace(c.CommandName))
                         .GroupBy(c => c.CommandName, StringComparer.OrdinalIgnoreCase))
            {
                var first = group.First();
                if (group.Count() > 1)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.CommandDuplicate,
                        Severity = ValidationSeverity.Error,
                        EntityType = "Command",
                        EntityId = first.Id,
                        EntityName = first.CommandName,
                        Message = $"{group.Count()} action commands share the name '!{first.CommandName}', so only one of them can respond.",
                        Suggestion = "Rename or delete the duplicates.",
                        Link = "/actions/commands"
                    });
                }

                if (defaultNames.Contains(first.CommandName))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.CommandConflictsWithDefault,
                        Severity = ValidationSeverity.Warning,
                        EntityType = "Command",
                        EntityId = first.Id,
                        EntityName = first.CommandName,
                        Message = $"Action command '!{first.CommandName}' has the same name as a built-in command.",
                        Suggestion = "Rename one of them so viewers always get the response you expect.",
                        Link = "/actions/commands"
                    });
                }
            }

            foreach (var command in commands)
            {
                if ((command.UserCooldownMax != 0 && command.UserCooldownMax < command.UserCooldown) ||
                    (command.GlobalCooldownMax != 0 && command.GlobalCooldownMax < command.GlobalCooldown))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.CommandInvalidCooldown,
                        Severity = ValidationSeverity.Warning,
                        EntityType = "Command",
                        EntityId = command.Id,
                        EntityName = command.CommandName,
                        Message = $"Command '!{command.CommandName}' has a maximum cooldown lower than its minimum.",
                        Suggestion = "Set the maximum cooldown to at least the minimum (or 0 for a fixed cooldown).",
                        Link = "/actions/commands"
                    });
                }
            }

            var disabledIds = commands.Where(c => c.Disabled).Select(c => c.Id).ToHashSet();
            foreach (var action in actions.Where(a => a.Enabled))
            {
                foreach (var trigger in action.Triggers.Where(t => t.Enabled && t.Type == TriggerTypes.Command
                             && t.CommandId.HasValue && disabledIds.Contains(t.CommandId.Value)))
                {
                    result.Issues.Add(ForAction(action, ValidationIssueType.CommandDisabledButTriggered, ValidationSeverity.Warning,
                        $"Action '{action.Name}' is triggered by command '{trigger.Name}', but that command is disabled.",
                        "Enable the command, or remove the trigger.",
                        "/actions/commands", trigger.Id.ToString()));
                }
            }
        }

        private static void CheckKeywords(List<Database.Bot.Models.Commands.ActionKeyword> keywords, ValidationResult result)
        {
            foreach (var keyword in keywords.Where(k => k.IsRegex && !string.IsNullOrWhiteSpace(k.CommandName)))
            {
                try
                {
                    _ = new Regex(keyword.CommandName, RegexOptions.None, TimeSpan.FromMilliseconds(250));
                }
                catch (ArgumentException ex)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.KeywordInvalidRegex,
                        Severity = ValidationSeverity.Error,
                        EntityType = "Keyword",
                        EntityId = keyword.Id,
                        EntityName = keyword.CommandName,
                        Message = $"Keyword '{keyword.CommandName}' is not a valid regular expression: {ex.Message}",
                        Suggestion = "Fix the pattern, or turn off the regex option for this keyword.",
                        Link = "/actions/keywords"
                    });
                }
            }

            foreach (var group in keywords.Where(k => !string.IsNullOrWhiteSpace(k.CommandName))
                         .GroupBy(k => (k.CommandName.ToLowerInvariant(), k.IsRegex)))
            {
                if (group.Count() < 2) continue;
                var first = group.First();
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.KeywordDuplicate,
                    Severity = ValidationSeverity.Warning,
                    EntityType = "Keyword",
                    EntityId = first.Id,
                    EntityName = first.CommandName,
                    Message = $"{group.Count()} keywords use the same pattern '{first.CommandName}', so each chat message will trigger all of them.",
                    Suggestion = "Delete the duplicates, or make the patterns different.",
                    Link = "/actions/keywords"
                });
            }
        }

        private static void CheckTimerGroups(
            List<Database.Bot.Actions.ActionType> actions,
            List<Database.Bot.Models.Timers.TimerGroup> groups,
            ValidationResult result)
        {
            var actionCountByGroup = actions
                .Where(a => a.Enabled)
                .SelectMany(a => a.Triggers.Where(t => t.Enabled && t.Type == TriggerTypes.Timer && t.TimerGroupId.HasValue))
                .GroupBy(t => t.TimerGroupId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var group in groups)
            {
                var count = actionCountByGroup.GetValueOrDefault(group.Id);

                if (group.IntervalMinimumSeconds > group.IntervalMaximumSeconds)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.TimerGroupInvalidInterval,
                        Severity = ValidationSeverity.Error,
                        EntityType = "Timer",
                        EntityId = group.Id,
                        EntityName = group.Name,
                        Message = $"Timer group '{group.Name}' has a minimum interval ({group.IntervalMinimumSeconds}s) longer than its maximum ({group.IntervalMaximumSeconds}s).",
                        Suggestion = "Swap or correct the interval values.",
                        Link = "/actions/timers"
                    });
                }

                if (group.Active && count == 0)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.TimerGroupNoActions,
                        Severity = ValidationSeverity.Info,
                        EntityType = "Timer",
                        EntityId = group.Id,
                        EntityName = group.Name,
                        Message = $"Timer group '{group.Name}' is active but has no enabled actions to run.",
                        Suggestion = "Add an action to the group or deactivate it.",
                        Link = "/actions/timers"
                    });
                }
                else if (!group.Active && count > 0)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        IssueType = ValidationIssueType.TimerGroupInactiveWithActions,
                        Severity = ValidationSeverity.Warning,
                        EntityType = "Timer",
                        EntityId = group.Id,
                        EntityName = group.Name,
                        Message = $"Timer group '{group.Name}' is inactive, so its {count} action(s) will not run on a timer.",
                        Suggestion = "Activate the timer group if you want those actions to run.",
                        Link = "/actions/timers"
                    });
                }
            }
        }

        private static async Task CheckPointTypeSettings(
            List<Database.Bot.Models.Points.PointType> pointTypes,
            ValidationResult result,
            IUnitOfWork db)
        {
            var validIds = pointTypes.Select(p => p.Id).ToHashSet();
            var settings = await db.GameSettings.GetAsync(x => x.SettingName.ToLower() == "pointtypeid");

            foreach (var setting in settings)
            {
                if (UuidMigrationHelper.TryParseGuidOrLegacyInt(setting.SettingStringValue, out var id) && validIds.Contains(id))
                    continue;

                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.GameSettingPointTypeMissing,
                    Severity = ValidationSeverity.Error,
                    EntityType = "Settings",
                    EntityName = setting.GameName,
                    Message = $"The point type for '{setting.GameName}' is missing or no longer exists, so the first point type is used instead.",
                    Suggestion = "Open Game Settings and choose a point type for this game.",
                    Link = "/settings/games"
                });
            }
        }

        private static async Task CheckRaidRewards(
            IRaidRewardSettingsService raidSettings,
            List<Database.Bot.Models.Points.PointType> pointTypes,
            ValidationResult result)
        {
            var config = await raidSettings.GetConfigAsync();
            if (!config.Enabled) return;

            if (config.PointTypeId == Guid.Empty || pointTypes.All(p => p.Id != config.PointTypeId))
            {
                result.Issues.Add(new ValidationIssue
                {
                    IssueType = ValidationIssueType.RaidRewardPointTypeMissing,
                    Severity = ValidationSeverity.Error,
                    EntityType = "Settings",
                    EntityName = "Raid Rewards",
                    Message = "Raid rewards are enabled but the selected point type is missing or no longer exists.",
                    Suggestion = "Open Raid Rewards and choose a point type.",
                    Link = "/settings/raidrewards"
                });
            }
        }
    }
}
