using Bunit;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Moq;
using PenguinTwitchBot.Pages.Actions;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Models.Queues;
using Microsoft.AspNetCore.Components;

namespace PenguinTwitchBot.Test.Pages.Actions
{
    public class ActionHistoryTests : IAsyncLifetime
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

        private static List<ActionExecutionLog> CreateTestLogs()
        {
            return
            [
                new ActionExecutionLog
                {
                    Id = Guid.NewGuid(),
                    ActionName = "TestAction1",
                    State = ActionExecutionState.Completed,
                    QueueName = "Default",
                    EnqueuedAt = DateTime.UtcNow.AddMinutes(-5),
                    StartedAt = DateTime.UtcNow.AddMinutes(-4),
                    CompletedAt = DateTime.UtcNow.AddMinutes(-3),
                    VariablesBefore = new Dictionary<string, string> { { "var1", "value1" } },
                    VariablesAfter = new Dictionary<string, string> { { "var1", "value2" } },
                    SubActionLogs = new List<SubActionExecutionLog>(),
                    ChildActionLogIds = new List<Guid>()
                },
                new ActionExecutionLog
                {
                    Id = Guid.NewGuid(),
                    ActionName = "TestAction2",
                    State = ActionExecutionState.Failed,
                    QueueName = "HighPriority",
                    EnqueuedAt = DateTime.UtcNow.AddMinutes(-10),
                    StartedAt = DateTime.UtcNow.AddMinutes(-9),
                    CompletedAt = DateTime.UtcNow.AddMinutes(-8),
                    ErrorMessage = "Something went wrong",
                    VariablesBefore = new Dictionary<string, string>(),
                    VariablesAfter = null,
                    SubActionLogs = new List<SubActionExecutionLog>(),
                    ChildActionLogIds = new List<Guid>()
                },
                new ActionExecutionLog
                {
                    Id = Guid.NewGuid(),
                    ActionName = "TestAction3",
                    State = ActionExecutionState.Running,
                    QueueName = "Default",
                    EnqueuedAt = DateTime.UtcNow.AddMinutes(-1),
                    StartedAt = DateTime.UtcNow.AddSeconds(-30),
                    CompletedAt = null,
                    VariablesBefore = new Dictionary<string, string> { { "var1", "value1" } },
                    VariablesAfter = null,
                    SubActionLogs = new List<SubActionExecutionLog>(),
                    ChildActionLogIds = new List<Guid>()
                }
            ];
        }

        private void SetupServices(List<ActionExecutionLog>? logs = null)
        {
            logs ??= CreateTestLogs();

            var mockQueueManager = new Mock<IQueueManager>();
            mockQueueManager.Setup(q => q.ExecutionLogger).Returns(new Mock<IActionExecutionLogger>().Object);

            var mockLogger = new Mock<IActionExecutionLogger>();
            mockLogger.Setup(l => l.GetRecentLogs(It.IsAny<int>())).Returns(logs);
            mockLogger.Setup(l => l.GetLogCount()).Returns(logs.Count);
            mockLogger.Setup(l => l.MaxLogCount()).Returns(1000);

            mockQueueManager.Setup(q => q.ExecutionLogger).Returns(mockLogger.Object);

            var mockDialogService = new Mock<IDialogService>();

            var mockSnackbar = new Mock<ISnackbar>();

            _ctx!.Services.AddSingleton<IQueueManager>(mockQueueManager.Object);
            _ctx.Services.AddSingleton<IActionExecutionLogger>(mockLogger.Object);
            _ctx.Services.AddSingleton<IDialogService>(mockDialogService.Object);
            _ctx.Services.AddSingleton<ISnackbar>(mockSnackbar.Object);
        }

        private IRenderedComponent<ActionHistory> RenderPage()
        {
            return _ctx!.Render<ActionHistory>();
        }

        [Fact]
        public async Task Page_Renders_WithTitle()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Action Execution History", page.Markup);
                Assert.Contains("Execution Logs", page.Markup);
                Assert.Contains("TestAction1", page.Markup);
                Assert.Contains("TestAction2", page.Markup);
                Assert.Contains("TestAction3", page.Markup);
            });
        }

        [Fact]
        public async Task EmptyState_ShowsNoLogs()
        {
            SetupContext();
            SetupServices(new List<ActionExecutionLog>());
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("No execution logs found", page.Markup);
            });
        }

        [Fact]
        public async Task Loading_ShowsSpinner()
        {
            SetupContext();
            var mockLogger = new Mock<IActionExecutionLogger>();
            mockLogger.Setup(l => l.GetLogCount()).Returns(0);
            mockLogger.Setup(l => l.MaxLogCount()).Returns(1000);
            mockLogger.Setup(l => l.GetRecentLogs(It.IsAny<int>())).Returns(new List<ActionExecutionLog>());

            var mockQueueManager = new Mock<IQueueManager>();
            mockQueueManager.Setup(q => q.ExecutionLogger).Returns(mockLogger.Object);

            _ctx!.Services.AddSingleton<IQueueManager>(mockQueueManager.Object);
            _ctx.Services.AddSingleton<IActionExecutionLogger>(mockLogger.Object);
            _ctx.Services.AddSingleton<IDialogService>(new Mock<IDialogService>().Object);
            _ctx.Services.AddSingleton<ISnackbar>(new Mock<ISnackbar>().Object);

            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Execution Logs", page.Markup);
            });
        }

        [Fact]
        public async Task Logs_DisplayCorrectStates()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Completed", page.Markup);
                Assert.Contains("Failed", page.Markup);
                Assert.Contains("Running", page.Markup);
            });
        }

        [Fact]
        public async Task Search_FiltersLogs()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("TestAction1", page.Markup);
                Assert.Contains("TestAction2", page.Markup);
            });

            var searchInput = page.Find(".search-log-input input");
            searchInput.Input("TestAction1");

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("TestAction1", page.Markup);
                Assert.DoesNotContain("TestAction2", page.Markup);
            });
        }

        [Fact]
        public async Task StateFilter_SelectRenders()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Filter by State", page.Markup);
            });
        }

        [Fact]
        public async Task Refresh_ReloadsLogs()
        {
            SetupContext();
            var logs = CreateTestLogs();
            var mockLogger = new Mock<IActionExecutionLogger>();
            mockLogger.Setup(l => l.GetRecentLogs(It.IsAny<int>())).Returns(logs);
            mockLogger.Setup(l => l.GetLogCount()).Returns(logs.Count);
            mockLogger.Setup(l => l.MaxLogCount()).Returns(1000);

            var mockQueueManager = new Mock<IQueueManager>();
            mockQueueManager.Setup(q => q.ExecutionLogger).Returns(mockLogger.Object);

            _ctx!.Services.AddSingleton<IQueueManager>(mockQueueManager.Object);
            _ctx.Services.AddSingleton<IActionExecutionLogger>(mockLogger.Object);
            _ctx.Services.AddSingleton<IDialogService>(new Mock<IDialogService>().Object);
            _ctx.Services.AddSingleton<ISnackbar>(new Mock<ISnackbar>().Object);

            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("TestAction1", page.Markup);
            });

            mockLogger.Verify(l => l.GetRecentLogs(It.IsAny<int>()), Times.Once);

            var refreshButton = page.Find(".refresh-logs-btn");
            refreshButton.Click();

            await page.WaitForAssertionAsync(() =>
            {
                mockLogger.Verify(l => l.GetRecentLogs(It.IsAny<int>()), Times.Exactly(2));
            });
        }

        [Fact]
        public async Task ViewDetails_ButtonExists()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("TestAction1", page.Markup);
            });

            var viewButton = page.Find(".view-details-btn");
            Assert.NotNull(viewButton);
            Assert.Contains("View Details", viewButton.TextContent);
        }

        [Fact]
        public async Task LogRow_ShowsDetails()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("TestAction1", page.Markup);
            });

            var logRow = page.Find(".mud-table-row:contains('TestAction1')");
            Assert.NotNull(logRow);
            Assert.Contains("Default", logRow.TextContent);
            Assert.Contains("Completed", logRow.TextContent);
        }

        [Fact]
        public async Task PauseButton_ExistsAndClickable()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                var pauseButton = page.Find(".pause-updates-btn");
                Assert.NotNull(pauseButton);
            });

            var pauseButton = page.Find(".pause-updates-btn");
            pauseButton.Click();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Resume", page.Markup);
            });
        }

        [Fact]
        public async Task Logs_ShowQueueName()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Default", page.Markup);
                Assert.Contains("HighPriority", page.Markup);
            });
        }

        [Fact]
        public async Task Logs_WithVariables_ShowsCount()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("1/1", page.Markup);
            });
        }

        [Fact]
        public async Task FailedLog_ShowsFailedState()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Failed", page.Markup);
            });
        }

        [Fact]
        public async Task CompletedLog_ShowsDuration()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("2.00m", page.Markup);
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
