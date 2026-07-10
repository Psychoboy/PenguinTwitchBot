using Bunit;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Components;
using Moq;
using PenguinTwitchBot.Pages.Commands;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Test.Pages.Commands
{
    public class ActionCommandsTests : IAsyncLifetime
    {
        private BunitContext? _ctx;

        public Task InitializeAsync() => Task.CompletedTask;

        private void SetupContext()
        {
            _ctx = new BunitContext();
            _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            _ctx.Services.AddMudServices(options =>
            {
                options.PopoverOptions.CheckForPopoverProvider = false;
            });

            var configuration = new ConfigurationBuilder().Build();
            _ctx.Services.AddSingleton<IConfiguration>(configuration);
        }

        private static List<ActionCommand> CreateTestCommands()
        {
            return
            [
                new ActionCommand
                {
                    Id = 1,
                    CommandName = "test",
                    Category = "Test Category",
                    Description = "A test command",
                    Disabled = false,
                    UserCooldown = 5,
                    GlobalCooldown = 10,
                    Cost = 0,
                    MinimumRank = PenguinTwitchBot.Database.Bot.Models.Rank.Viewer,
                    SayCooldown = true,
                    SayRankRequirement = false,
                    ExcludeFromUi = false,
                    SourceOnly = true
                },
                new ActionCommand
                {
                    Id = 2,
                    CommandName = "hello",
                    Category = "Greetings",
                    Description = "Say hello",
                    Disabled = true,
                    UserCooldown = 0,
                    GlobalCooldown = 0,
                    Cost = 0,
                    MinimumRank = PenguinTwitchBot.Database.Bot.Models.Rank.Viewer,
                    SayCooldown = false,
                    SayRankRequirement = false,
                    ExcludeFromUi = false,
                    SourceOnly = true
                },
                new ActionCommand
                {
                    Id = 3,
                    CommandName = "points",
                    Category = "Points",
                    Description = "Check points",
                    Disabled = false,
                    UserCooldown = 30,
                    GlobalCooldown = 60,
                    Cost = 0,
                    MinimumRank = PenguinTwitchBot.Database.Bot.Models.Rank.Viewer,
                    SayCooldown = true,
                    SayRankRequirement = false,
                    ExcludeFromUi = false,
                    SourceOnly = true
                }
            ];
        }

        private void SetupServices(List<ActionCommand>? commands = null, List<TriggerType>? triggers = null, List<ActionType>? actions = null)
        {
            commands ??= CreateTestCommands();
            triggers ??= new List<TriggerType>();
            actions ??= new List<ActionType>();

            var mockCommandService = new Mock<IActionCommandService>();
            mockCommandService.Setup(s => s.GetAllAsync()).ReturnsAsync(commands);
            mockCommandService.Setup(s => s.GetByIdAsync(It.IsAny<int>())).Returns((int id) => Task.FromResult(commands.FirstOrDefault(c => c.Id == id)));
            mockCommandService.Setup(s => s.CommandExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            mockCommandService.Setup(s => s.GetByCommandNameAsync(It.IsAny<string>())).Returns((string name) => Task.FromResult(commands.FirstOrDefault(c => c.CommandName.Equals(name, StringComparison.OrdinalIgnoreCase))));

            var mockActionService = new Mock<IActionManagementService>();
            mockActionService.Setup(s => s.GetAllTriggersAsync()).ReturnsAsync(triggers);
            mockActionService.Setup(s => s.GetAllActionsAsync()).ReturnsAsync(actions);

            var mockPointSystem = new Mock<IPointsSystem>();
            mockPointSystem.Setup(s => s.GetPointTypes()).ReturnsAsync(new List<PointType>());

            var mockDialogService = new Mock<IDialogService>();
            mockDialogService.Setup(s => s.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DialogOptions?>()))
                .Returns(Task.FromResult<bool?>(true));

            var mockSnackbar = new Mock<ISnackbar>();

            _ctx!.Services.AddSingleton<IActionCommandService>(mockCommandService.Object);
            _ctx.Services.AddSingleton<IActionManagementService>(mockActionService.Object);
            _ctx.Services.AddSingleton<IPointsSystem>(mockPointSystem.Object);
            _ctx.Services.AddSingleton<IDialogService>(mockDialogService.Object);
            _ctx.Services.AddSingleton<ISnackbar>(mockSnackbar.Object);
        }

        private IRenderedComponent<ActionCommands> RenderPage()
        {
            return _ctx!.Render<ActionCommands>();
        }

        [Fact]
        public async Task Page_Renders_WithCommands()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Action Commands", page.Markup);
                Assert.Contains("Commands (3)", page.Markup);
                Assert.Contains("!test", page.Markup);
                Assert.Contains("!hello", page.Markup);
                Assert.Contains("!points", page.Markup);
            });
        }

        [Fact]
        public async Task EmptyState_ShowsNoCommands()
        {
            SetupContext();
            SetupServices(new List<ActionCommand>());
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("No commands found", page.Markup);
            });
        }

        [Fact]
        public async Task CreateNewCommand_ShowsEditor()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Commands (3)", page.Markup);
            });

            var addButton = page.Find(".add-command-btn");
            addButton.Click();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("New Command", page.Markup);
                Assert.Contains("Save", page.Markup);
            });
        }

        [Fact]
        public async Task SelectCommand_ShowsDetails()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("!test", page.Markup);
            });

            var testCommand = page.Find(".command-item:contains('!test')");
            testCommand.Click();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Edit Command: !test", page.Markup);
                Assert.Contains("A test command", page.Markup);
                Assert.Contains("Test Category", page.Markup);
            });
        }

        [Fact]
        public async Task CancelEdit_ClearsSelection()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("!test", page.Markup);
            });

            var testCommand = page.Find(".command-item:contains('!test')");
            testCommand.Click();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Edit Command: !test", page.Markup);
            });

            var cancelButton = page.Find("button:contains('Cancel')");
            cancelButton.Click();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Select a command to view details", page.Markup);
                Assert.DoesNotContain("Edit Command: !test", page.Markup);
            });
        }

        [Fact]
        public async Task TriggeredActions_LoadsAndDisplays()
        {
            SetupContext();
            var commands = CreateTestCommands();
            var triggers = new List<TriggerType>
            {
                new TriggerType
                {
                    Id = 1,
                    Type = TriggerTypes.Command,
                    CommandId = 1,
                    ActionId = 10,
                    Enabled = true,
                    Name = "Test Trigger"
                }
            };
            var actions = new List<ActionType>
            {
                new ActionType
                {
                    Id = 10,
                    Name = "Test Action",
                    Group = "Test Group",
                    Enabled = true,
                    SubActions = new List<SubActionType>(),
                    Triggers = new List<TriggerType>()
                }
            };

            SetupServices(commands, triggers, actions);
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("!test", page.Markup);
            });

            var testCommand = page.Find(".command-item:contains('!test')");
            testCommand.Click();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Actions Triggered by This Command", page.Markup);
                Assert.Contains("Test Action", page.Markup);
                Assert.Contains("Test Group", page.Markup);
            });
        }

        [Fact]
        public async Task NoTriggeredActions_ShowsInfoMessage()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("!hello", page.Markup);
            });

            var helloCommand = page.Find(".command-item:contains('!hello')");
            helloCommand.Click();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("This command does not trigger any actions yet", page.Markup);
            });
        }

        [Fact]
        public async Task DisabledCommand_ShowsErrorChip()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("!hello", page.Markup);
                Assert.Contains("Disabled", page.Markup);
            });
        }

        [Fact]
        public async Task EnabledCommand_ShowsSuccessChip()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("!test", page.Markup);
                Assert.Contains("Enabled", page.Markup);
            });
        }

        [Fact]
        public async Task OnLocationChanged_SelectsCommandFromQueryString()
        {
            SetupContext();
            SetupServices();
            var navManager = _ctx!.Services.GetRequiredService<NavigationManager>();
            var page = RenderPage();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("!test", page.Markup);
            });

            navManager.NavigateTo("/actions/commands?commandId=2");

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Edit Command: !hello", page.Markup);
                Assert.Contains("Say hello", page.Markup);
            });
        }

        [Fact]
        public async Task DeleteCommand_Click_TriggersServiceCall()
        {
            SetupContext();
            var commands = CreateTestCommands();
            var deleteCalled = false;
            var deletedId = 0;
            var commandService = new Mock<IActionCommandService>();
            commandService.Setup(s => s.GetAllAsync()).ReturnsAsync(commands);
            commandService.Setup(s => s.DeleteAsync(It.IsAny<int>())).Callback<int>(id =>
            {
                deleteCalled = true;
                deletedId = id;
            }).Returns(Task.CompletedTask);

            var mockDialogService = new Mock<IDialogService>();
            mockDialogService.Setup(s => s.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DialogOptions?>()))
                .Returns((string title, string message, string yesText, string? noText, string? cancelText, DialogOptions? options) => 
                    Task.FromResult<bool?>(true));

            _ctx!.Services.AddSingleton<IActionCommandService>(commandService.Object);
            _ctx.Services.AddSingleton<IActionManagementService>(new Mock<IActionManagementService>().Object);
            _ctx.Services.AddSingleton<IPointsSystem>(new Mock<IPointsSystem>().Object);
            _ctx.Services.AddSingleton<IDialogService>(mockDialogService.Object);
            _ctx.Services.AddSingleton<ISnackbar>(new Mock<ISnackbar>().Object);

            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("!test", page.Markup);
            });

            var testCommand = page.Find(".command-item:contains('!test')");
            testCommand.Click();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Edit Command: !test", page.Markup);
            });

            var deleteButton = page.Find(".delete-command-btn");
            deleteButton.Click();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.True(deleteCalled);
                Assert.Equal(1, deletedId);
            });
        }

        [Fact]
        public async Task CreateNewCommand_DuplicateName_ShowsWarning()
        {
            SetupContext();
            var commands = CreateTestCommands();
            var commandService = new Mock<IActionCommandService>();
            commandService.Setup(s => s.GetAllAsync()).ReturnsAsync(commands);
            commandService.Setup(s => s.CommandExistsAsync("test")).ReturnsAsync(true);

            var mockDialogService = new Mock<IDialogService>();
            mockDialogService.Setup(s => s.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DialogOptions?>()))
                .Returns((string title, string message, string yesText, string? noText, string? cancelText, DialogOptions? options) => 
                    Task.FromResult<bool?>(null));

            _ctx!.Services.AddSingleton<IActionCommandService>(commandService.Object);
            _ctx.Services.AddSingleton<IActionManagementService>(new Mock<IActionManagementService>().Object);
            _ctx.Services.AddSingleton<IPointsSystem>(new Mock<IPointsSystem>().Object);
            _ctx.Services.AddSingleton<IDialogService>(mockDialogService.Object);
            _ctx.Services.AddSingleton<ISnackbar>(new Mock<ISnackbar>().Object);

            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Commands (3)", page.Markup);
            });

            var addButton = page.Find(".add-command-btn");
            addButton.Click();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("New Command", page.Markup);
            });

            var nameInput = page.Find(".command-name-input input");
            nameInput.Change("test");

            var saveButton = page.Find(".save-command-btn");
            saveButton.Click();

            await page.WaitForAssertionAsync(() =>
            {
                mockDialogService.Verify(s => s.ShowMessageBoxAsync(
                    "Warning",
                    "This command already exists. Continue?",
                    "Yes", null, "Cancel", null), Times.Once);
                commandService.Verify(s => s.AddAsync(It.IsAny<ActionCommand>()), Times.Never);
            });
        }

        public async Task DisposeAsync()
        {
            if (_ctx is not null)
            {
                await _ctx.DisposeAsync();
            }
        }
    }
}
