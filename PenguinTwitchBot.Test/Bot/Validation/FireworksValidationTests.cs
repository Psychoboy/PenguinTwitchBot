using PenguinTwitchBot.Bot.Validation;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Overlay;

namespace PenguinTwitchBot.Test.Bot.Validation
{
    public class FireworksValidationTests
    {
        [Fact]
        public void CreateFireworksWidgetIssue_WarnsWhenConfiguredWithoutEnabledWidget()
        {
            var actions = new[]
            {
                new ActionType
                {
                    Id = Guid.NewGuid(),
                    Name = "Fireworks Show",
                    Enabled = true,
                    SubActions = [new FireworksType()]
                }
            };
            var layouts = new[]
            {
                new OverlayLayout
                {
                    Name = "Default",
                    Widgets = [new OverlayWidget { WidgetType = "fireworks", IsEnabled = false }]
                }
            };

            var issue = ValidationService.CreateFireworksWidgetIssue(actions, layouts);

            Assert.NotNull(issue);
            Assert.Equal(ValidationIssueType.FireworksWidgetMissing, issue.IssueType);
            Assert.Equal(ValidationSeverity.Warning, issue.Severity);
            Assert.Equal("/overlay/editor", issue.Link);
        }

        [Fact]
        public void CreateFireworksWidgetIssue_IsSilentWhenEnabledWidgetExistsOrActionIsDisabled()
        {
            var action = new ActionType
            {
                Name = "Fireworks Show",
                Enabled = true,
                SubActions = [new FireworksType()]
            };

            Assert.Null(ValidationService.CreateFireworksWidgetIssue(
                [action],
                [new OverlayLayout { Widgets = [new OverlayWidget { WidgetType = "FIREWORKS", IsEnabled = true }] }]));

            action.Enabled = false;
            Assert.Null(ValidationService.CreateFireworksWidgetIssue([action], []));
        }

        [Fact]
        public void CreateFireworksWidgetIssue_IgnoresFireworksInsideDisabledLogicBranch()
        {
            var logic = new LogicIfElseType
            {
                Enabled = false,
                TrueSubActions = [new FireworksType()]
            };
            var action = new ActionType
            {
                Name = "Fireworks Show",
                Enabled = true,
                SubActions = [logic]
            };

            Assert.Null(ValidationService.CreateFireworksWidgetIssue([action], []));

            logic.Enabled = true;
            Assert.NotNull(ValidationService.CreateFireworksWidgetIssue([action], []));
        }

        [Fact]
        public void CreateLegacyFireworksIssues_LinksLegacyPresetAndBaseActionWithManualGuidance()
        {
            var fireworksId = Guid.NewGuid();
            var loopId = Guid.NewGuid();
            var actions = new[]
            {
                new ActionType
                {
                    Id = fireworksId,
                    Name = "Fireworks",
                    SubActions = [new DelayType { Duration = "10" }]
                },
                new ActionType
                {
                    Id = loopId,
                    Name = "Fireworks - Multi",
                    SubActions =
                    [
                        new LogicIfElseType
                        {
                            LeftValue = "%cumulative%",
                            Operator = ComparisonOperator.LessThanOrEqual,
                            RightValue = "0",
                            TrueSubActions = [new BreakType()],
                            FalseSubActions =
                            [
                                new SetVariableType { Text = "cumulative", Value = "$math(%cumulative%-1)$" },
                                new ExecuteActionType { ActionId = fireworksId },
                                new DelayType { Duration = "100" },
                                new ExecuteActionType { ActionId = loopId }
                            ]
                        }
                    ]
                },
                new ActionType
                {
                    Id = Guid.NewGuid(),
                    Name = "Fireworks -25",
                    SubActions =
                    [
                        new SetVariableType { Text = "cumulative", Value = "25" },
                        new ExecuteActionType { ActionId = loopId }
                    ]
                }
            };

            var issues = ValidationService.CreateLegacyFireworksIssues(actions);
            var issue = Assert.Single(issues, candidate => candidate.EntityName == "Fireworks -25");

            Assert.Equal(ValidationIssueType.LegacyFireworksDetected, issue.IssueType);
            Assert.Equal($"/actions/manage?actionId={actions[2].Id}", issue.Link);
            Assert.Contains("one Fireworks sub-action with Burst Count 25", issue.Suggestion);
            Assert.Contains("delay from the legacy Fireworks loop action", issue.Suggestion);
            var legacyBaseIssue = Assert.Single(issues, candidate => candidate.EntityName == "Fireworks");
            Assert.Equal("legacy-action-name", legacyBaseIssue.Detail);
            Assert.Equal($"/actions/manage?actionId={actions[0].Id}", legacyBaseIssue.Link);
        }

        [Fact]
        public void CreateLegacyFireworksIssues_ReportsLoopHelperAndNestedCallers()
        {
            var fireworksId = Guid.NewGuid();
            var helperId = Guid.NewGuid();
            var callerId = Guid.NewGuid();
            var actions = new[]
            {
                new ActionType
                {
                    Id = fireworksId,
                    Name = "Fireworks",
                    SubActions = [new DelayType { Duration = "10" }]
                },
                new ActionType
                {
                    Id = helperId,
                    Name = "Fireworks - Multi",
                    SubActions = [new ExecuteActionType { ActionId = fireworksId }]
                },
                new ActionType
                {
                    Id = callerId,
                    Name = "Channel Point Celebration",
                    SubActions =
                    [
                        new LogicIfElseType
                        {
                            TrueSubActions = [new ExecuteActionType { ActionId = helperId }]
                        }
                    ]
                }
            };

            var issues = ValidationService.CreateLegacyFireworksIssues(actions);
            var helperIssue = Assert.Single(issues, issue => issue.EntityId == helperId);
            var callerIssue = Assert.Single(issues, issue => issue.EntityId == callerId);

            Assert.Equal("legacy-fireworks-loop-helper", helperIssue.Detail);
            Assert.Equal($"/actions/manage?actionId={helperId}", helperIssue.Link);
            Assert.Equal("calls-legacy-fireworks-loop", callerIssue.Detail);
            Assert.Equal($"/actions/manage?actionId={callerId}", callerIssue.Link);
        }

        [Fact]
        public void CreateLegacyFireworksIssues_GivesManualDirectionsForEpicPreset()
        {
            var action = new ActionType
            {
                Id = Guid.NewGuid(),
                Name = "Fireworks - Epic(250)",
                SubActions =
                [
                    new SetVariableType { Text = "cumulative", Value = "250" },
                    new ExecuteActionType { ActionId = Guid.NewGuid() },
                    new SendMessageType { Text = "extra custom step" }
                ]
            };

            var issue = Assert.Single(ValidationService.CreateLegacyFireworksIssues([action]));

            Assert.Equal(ValidationIssueType.LegacyFireworksDetected, issue.IssueType);
            Assert.Contains("250 bursts", issue.Suggestion);
            Assert.Contains("1 to 100", issue.Suggestion);
            Assert.Equal($"/actions/manage?actionId={action.Id}", issue.Link);
        }

        [Fact]
        public void CreateLegacyFireworksIssues_UsesEnabledNestedCumulativeValueInsteadOfName()
        {
            var action = new ActionType
            {
                Id = Guid.NewGuid(),
                Name = "Fireworks -25",
                SubActions =
                [
                    new LogicIfElseType
                    {
                        TrueSubActions =
                        [
                            new SetVariableType { Text = "cumulative", Value = "10" },
                            new ExecuteActionType { ActionId = Guid.NewGuid() }
                        ]
                    }
                ]
            };

            var issue = Assert.Single(ValidationService.CreateLegacyFireworksIssues([action]));

            Assert.Contains("Burst Count 10", issue.Suggestion);
            Assert.DoesNotContain("Burst Count 25", issue.Suggestion);
        }

        [Fact]
        public void CreateLegacyFireworksIssues_DynamicCumulativeValueDoesNotSuggestNameCount()
        {
            var dynamicValue = "$math(%Amount% + 1)$";
            var action = new ActionType
            {
                Id = Guid.NewGuid(),
                Name = "Fireworks -25",
                SubActions =
                [
                    new SetVariableType { Text = "cumulative", Value = dynamicValue },
                    new ExecuteActionType { ActionId = Guid.NewGuid() }
                ]
            };

            var issue = Assert.Single(ValidationService.CreateLegacyFireworksIssues([action]));

            Assert.Contains(dynamicValue, issue.Suggestion);
            Assert.DoesNotContain("Burst Count 25", issue.Suggestion);
        }

        [Fact]
        public void CreateLegacyFireworksIssues_UnsupportedNameCountDoesNotPreventLaterWarnings()
        {
            var actions = new[]
            {
                new ActionType
                {
                    Id = Guid.NewGuid(),
                    Name = "Fireworks - 999999999999999999999999999999",
                    SubActions =
                    [
                        new SetVariableType { Text = "cumulative", Value = "$math(%Amount%)$" },
                        new ExecuteActionType { ActionId = Guid.NewGuid() }
                    ]
                },
                new ActionType
                {
                    Id = Guid.NewGuid(),
                    Name = "Fireworks -25",
                    SubActions =
                    [
                        new SetVariableType { Text = "cumulative", Value = "25" },
                        new ExecuteActionType { ActionId = Guid.NewGuid() }
                    ]
                }
            };

            var issues = ValidationService.CreateLegacyFireworksIssues(actions);

            Assert.Equal(2, issues.Count);
            Assert.Contains(issues, issue => issue.EntityId == actions[0].Id &&
                issue.Detail == "legacy-preset-unsupported-count");
            Assert.Contains(issues, issue => issue.EntityId == actions[1].Id &&
                issue.Suggestion?.Contains("Burst Count 25", StringComparison.Ordinal) == true);
        }
    }
}