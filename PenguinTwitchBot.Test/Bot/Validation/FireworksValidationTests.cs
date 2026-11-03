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
    }
}