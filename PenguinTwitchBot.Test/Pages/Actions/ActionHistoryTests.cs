using Bunit;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Moq;
using PenguinTwitchBot.Pages.Actions;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Database.Bot.Models.Queues;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Reflection;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging.Abstractions;

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

            var mockHubFactory = new Mock<ISignalRHubConnectionFactory>();

            _ctx!.Services.AddSingleton<IQueueManager>(mockQueueManager.Object);
            _ctx.Services.AddSingleton<IActionExecutionLogger>(mockLogger.Object);
            _ctx.Services.AddSingleton<IDialogService>(mockDialogService.Object);
            _ctx.Services.AddSingleton<ISnackbar>(mockSnackbar.Object);
            _ctx.Services.AddSingleton<ISignalRHubConnectionFactory>(mockHubFactory.Object);
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
            _ctx.Services.AddSingleton<ISignalRHubConnectionFactory>(new Mock<ISignalRHubConnectionFactory>().Object);

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

            var component = page.Instance;
            var filterLogsMethod = typeof(ActionHistory).GetMethod("FilterLogs", BindingFlags.Instance | BindingFlags.NonPublic)!;
            filterLogsMethod.Invoke(component, null);
            page.Render();

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
            _ctx.Services.AddSingleton<ISignalRHubConnectionFactory>(new Mock<ISignalRHubConnectionFactory>().Object);

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

        [Fact]
        public async Task PageTitle_RendersCorrectly()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Action Execution History", page.Markup);
            });
        }

        [Fact]
        public async Task PausedUpdates_WithPendingCount_ShowsBanner()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            var component = page.Instance;
            var field = typeof(ActionHistory).GetField("_updatesPaused", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var countField = typeof(ActionHistory).GetField("_pendingUpdatesCount", BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(component, true);
            countField.SetValue(component, 3);
            page.Render();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Real-time updates paused. 3 new logs waiting", page.Markup);
            });
        }

        [Fact]
        public async Task PausedUpdates_WithoutPending_ShowsGenericBanner()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            var component = page.Instance;
            var field = typeof(ActionHistory).GetField("_updatesPaused", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var countField = typeof(ActionHistory).GetField("_pendingUpdatesCount", BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(component, true);
            countField.SetValue(component, 0);
            page.Render();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Real-time updates paused. Click Resume in the toolbar", page.Markup);
            });
        }

        [Fact]
        public async Task MemoryWarning_ShowsWhenLogsExceed800()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            var component = page.Instance;
            var totalField = typeof(ActionHistory).GetField("_totalLogCount", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var maxField = typeof(ActionHistory).GetField("_maxLogs", BindingFlags.Instance | BindingFlags.NonPublic)!;
            totalField.SetValue(component, 900);
            maxField.SetValue(component, 1000);
            page.Render();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Memory contains 900 action logs", page.Markup);
            });
        }

        [Fact]
        public async Task LogRow_WithParentAction_ShowsIcon()
        {
            SetupContext();
            var logs = new List<ActionExecutionLog>
            {
                new ActionExecutionLog
                {
                    Id = Guid.NewGuid(),
                    ActionName = "ChildAction",
                    State = ActionExecutionState.Completed,
                    QueueName = "Default",
                    EnqueuedAt = DateTime.UtcNow.AddMinutes(-1),
                    StartedAt = DateTime.UtcNow.AddSeconds(-30),
                    CompletedAt = DateTime.UtcNow,
                    ParentActionLogId = Guid.NewGuid(),
                    VariablesBefore = new Dictionary<string, string>(),
                    VariablesAfter = new Dictionary<string, string>(),
                    SubActionLogs = new List<SubActionExecutionLog>(),
                    ChildActionLogIds = new List<Guid>()
                }
            };
            SetupServices(logs);
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("ChildAction", page.Markup);
                Assert.Contains("child-action-icon", page.Markup);
            });
        }

        [Fact]
        public async Task LogRow_WithSubActions_ShowsChip()
        {
            SetupContext();
            var logs = new List<ActionExecutionLog>
            {
                new ActionExecutionLog
                {
                    Id = Guid.NewGuid(),
                    ActionName = "ActionWithSubs",
                    State = ActionExecutionState.Completed,
                    QueueName = "Default",
                    EnqueuedAt = DateTime.UtcNow.AddMinutes(-1),
                    StartedAt = DateTime.UtcNow.AddSeconds(-30),
                    CompletedAt = DateTime.UtcNow,
                    SubActionLogs = new List<SubActionExecutionLog>
                    {
                        new SubActionExecutionLog(),
                        new SubActionExecutionLog()
                    },
                    VariablesBefore = new Dictionary<string, string>(),
                    VariablesAfter = new Dictionary<string, string>(),
                    ChildActionLogIds = new List<Guid>()
                }
            };
            SetupServices(logs);
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("2 SubActions", page.Markup);
                Assert.Contains("subactions-chip", page.Markup);
            });
        }

        [Fact]
        public async Task LogRow_WithChildren_ShowsChip()
        {
            SetupContext();
            var logs = new List<ActionExecutionLog>
            {
                new ActionExecutionLog
                {
                    Id = Guid.NewGuid(),
                    ActionName = "ParentAction",
                    State = ActionExecutionState.Completed,
                    QueueName = "Default",
                    EnqueuedAt = DateTime.UtcNow.AddMinutes(-1),
                    StartedAt = DateTime.UtcNow.AddSeconds(-30),
                    CompletedAt = DateTime.UtcNow,
                    ChildActionLogIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
                    VariablesBefore = new Dictionary<string, string>(),
                    VariablesAfter = new Dictionary<string, string>(),
                    SubActionLogs = new List<SubActionExecutionLog>()
                }
            };
            SetupServices(logs);
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("2 Children", page.Markup);
                Assert.Contains("children-chip", page.Markup);
            });
        }

        [Fact]
        public async Task LogRow_NoDuration_ShowsDash()
        {
            SetupContext();
            var logs = new List<ActionExecutionLog>
            {
                new ActionExecutionLog
                {
                    Id = Guid.NewGuid(),
                    ActionName = "NoDurationAction",
                    State = ActionExecutionState.Running,
                    QueueName = "Default",
                    EnqueuedAt = DateTime.UtcNow.AddMinutes(-1),
                    StartedAt = null,
                    CompletedAt = null,
                    VariablesBefore = new Dictionary<string, string>(),
                    VariablesAfter = null,
                    SubActionLogs = new List<SubActionExecutionLog>(),
                    ChildActionLogIds = new List<Guid>()
                }
            };
            SetupServices(logs);
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("NoDurationAction", page.Markup);
            });
        }

        [Fact]
        public async Task LoadLogs_Error_DoesNotThrow()
        {
            SetupContext();
            var mockLogger = new Mock<IActionExecutionLogger>();
            mockLogger.Setup(l => l.GetLogCount()).Returns(1);
            mockLogger.Setup(l => l.MaxLogCount()).Returns(1000);
            mockLogger.Setup(l => l.GetRecentLogs(It.IsAny<int>())).Throws(new InvalidOperationException("DB error"));

            var mockQueueManager = new Mock<IQueueManager>();
            mockQueueManager.Setup(q => q.ExecutionLogger).Returns(mockLogger.Object);

            _ctx!.Services.AddSingleton<IQueueManager>(mockQueueManager.Object);
            _ctx.Services.AddSingleton<IActionExecutionLogger>(mockLogger.Object);
            _ctx.Services.AddSingleton<IDialogService>(new Mock<IDialogService>().Object);
            _ctx.Services.AddSingleton<ISnackbar>(new Mock<ISnackbar>().Object);
            _ctx.Services.AddSingleton<ISignalRHubConnectionFactory>(new Mock<ISignalRHubConnectionFactory>().Object);

            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Execution Logs", page.Markup);
            });
        }

        [Fact]
        public async Task PauseButton_TogglesUpdates()
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
        public async Task ViewDetails_OpensDialog()
        {
            SetupContext();
            var logs = CreateTestLogs();
            var mockLogger = new Mock<IActionExecutionLogger>();
            mockLogger.Setup(l => l.GetRecentLogs(It.IsAny<int>())).Returns(logs);
            mockLogger.Setup(l => l.GetLogCount()).Returns(logs.Count);
            mockLogger.Setup(l => l.MaxLogCount()).Returns(1000);

            var mockQueueManager = new Mock<IQueueManager>();
            mockQueueManager.Setup(q => q.ExecutionLogger).Returns(mockLogger.Object);

            var mockDialogService = new Mock<IDialogService>();
            var mockDialogReference = new Mock<IDialogReference>();
            mockDialogReference.SetupGet(r => r.Result).Returns(Task.FromResult<DialogResult?>(DialogResult.Ok(true)));
            mockDialogService
                .Setup(s => s.ShowAsync<ActionVariablesDialog>(It.IsAny<string>(), It.IsAny<DialogParameters<ActionVariablesDialog>>(), It.IsAny<DialogOptions>()))
                .ReturnsAsync(mockDialogReference.Object);

            _ctx!.Services.AddSingleton<IQueueManager>(mockQueueManager.Object);
            _ctx.Services.AddSingleton<IActionExecutionLogger>(mockLogger.Object);
            _ctx.Services.AddSingleton<IDialogService>(mockDialogService.Object);
            _ctx.Services.AddSingleton<ISnackbar>(new Mock<ISnackbar>().Object);
            _ctx.Services.AddSingleton<ISignalRHubConnectionFactory>(new Mock<ISignalRHubConnectionFactory>().Object);

            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("TestAction1", page.Markup);
            });

            var viewButton = page.Find(".view-details-btn");
            viewButton.Click();

            mockDialogService.Verify(s => s.ShowAsync<ActionVariablesDialog>(
                "Action Execution Details",
                It.IsAny<DialogParameters<ActionVariablesDialog>>(),
                It.IsAny<DialogOptions>()), Times.Once);
        }

        [Fact]
        public async Task OnFilterStateChanged_FiltersByState()
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
            _ctx.Services.AddSingleton<ISignalRHubConnectionFactory>(new Mock<ISignalRHubConnectionFactory>().Object);

            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("TestAction1", page.Markup);
                Assert.Contains("TestAction2", page.Markup);
            });

            var component = page.Instance;
            var method = typeof(ActionHistory).GetMethod("OnFilterStateChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await _ctx!.Renderer.Dispatcher.InvokeAsync(() => method.Invoke(component, new object[] { ActionExecutionState.Failed }));
            page.Render();

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("TestAction2", page.Markup);
                Assert.DoesNotContain("TestAction1", page.Markup);
            });
        }

        [Fact]
        public async Task TogglePauseUpdates_TogglesState()
        {
            SetupContext();
            SetupServices();
            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("TestAction1", page.Markup);
            });

            var component = page.Instance;
            var pausedField = typeof(ActionHistory).GetField("_updatesPaused", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.False((bool)pausedField.GetValue(component)!);

            var toggleMethod = typeof(ActionHistory).GetMethod("TogglePauseUpdates", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await _ctx!.Renderer.Dispatcher.InvokeAsync(() => toggleMethod.Invoke(component, null));
            page.Render();

            Assert.True((bool)pausedField.GetValue(component)!);

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Resume", page.Markup);
            });

            await _ctx!.Renderer.Dispatcher.InvokeAsync(() => toggleMethod.Invoke(component, null));
            page.Render();
            Assert.False((bool)pausedField.GetValue(component)!);
        }

        [Fact]
        public async Task ResumeUpdates_ClearsPendingAndReloads()
        {
            SetupContext();
            var logs = CreateTestLogs();
            var mockLogger = new Mock<IActionExecutionLogger>();
            mockLogger.Setup(l => l.GetRecentLogs(It.IsAny<int>())).Returns(logs);
            mockLogger.Setup(l => l.GetLogCount()).Returns(logs.Count);
            mockLogger.Setup(l => l.MaxLogCount()).Returns(1000);

            var mockQueueManager = new Mock<IQueueManager>();
            mockQueueManager.Setup(q => q.ExecutionLogger).Returns(mockLogger.Object);

            var mockDialogService = new Mock<IDialogService>();

            _ctx!.Services.AddSingleton<IQueueManager>(mockQueueManager.Object);
            _ctx.Services.AddSingleton<IActionExecutionLogger>(mockLogger.Object);
            _ctx.Services.AddSingleton<IDialogService>(mockDialogService.Object);
            _ctx.Services.AddSingleton<ISnackbar>(new Mock<ISnackbar>().Object);
            _ctx.Services.AddSingleton<ISignalRHubConnectionFactory>(new Mock<ISignalRHubConnectionFactory>().Object);

            var page = RenderPage();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("TestAction1", page.Markup);
            });

            var component = page.Instance;
            var pausedField = typeof(ActionHistory).GetField("_updatesPaused", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var pendingField = typeof(ActionHistory).GetField("_pendingUpdatesCount", BindingFlags.Instance | BindingFlags.NonPublic)!;
            pausedField.SetValue(component, true);
            pendingField.SetValue(component, 5);
            page.Render();

            mockLogger.Invocations.Clear();
            var toggleMethod = typeof(ActionHistory).GetMethod("TogglePauseUpdates", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await _ctx!.Renderer.Dispatcher.InvokeAsync(() => toggleMethod.Invoke(component, null));
            page.Render();

            Assert.False((bool)pausedField.GetValue(component)!);
            Assert.Equal(0, (int)pendingField.GetValue(component)!);
            mockLogger.Verify(l => l.GetRecentLogs(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task SignalR_WhenNotPaused_ShouldInsertNewLogIntoUI()
        {
            // 1. Arrange: Context & Required page dependencies setup
            _ctx = new BunitContext();
            _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            _ctx.Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
            _ctx.Services.AddSingleton<ISnackbar>(new Mock<ISnackbar>().Object);
            _ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            var mockExecutionLogger = new Mock<IActionExecutionLogger>();
            _ctx.Services.AddSingleton<IActionExecutionLogger>(mockExecutionLogger.Object);
            var mockQueueManager = new Mock<IQueueManager>();

            // 2. Tell the QueueManager mock to return the logger mock instead of null
            mockQueueManager
                .Setup(q => q.ExecutionLogger)
                .Returns(mockExecutionLogger.Object);

            // 3. Register it into the context services container
            _ctx.Services.AddSingleton<IQueueManager>(mockQueueManager.Object);

            // 2. Intercept the action callback registered by the component
            Func<ActionExecutionLog, Task>? attachedActionLogUpdatedHandler = null;

            var mockHubConnection = new Mock<ISignalRHubConnection>();
            mockHubConnection
                .Setup(h => h.On<ActionExecutionLog>("ActionLogUpdated", It.IsAny<Func<ActionExecutionLog, Task>>()))
                .Callback<string, Func<ActionExecutionLog, Task>>((eventName, handler) => 
                {
                    attachedActionLogUpdatedHandler = handler; // Capture the page's subscription lambda
                });

            mockHubConnection.Setup(h => h.StartAsync()).Returns(Task.CompletedTask);

            var mockHubFactory = new Mock<ISignalRHubConnectionFactory>();
            mockHubFactory
                .Setup(f => f.CreateMainHubConnection(It.IsAny<Uri>()))
                .Returns(mockHubConnection.Object);

            _ctx.Services.AddSingleton<ISignalRHubConnectionFactory>(mockHubFactory.Object);

            // 3. Act: Render the page component. This executes OnAfterRenderAsync and binds the handler.
            var page = _ctx.Render<ActionHistory>();

            // Force internal logs list to be initialized so the UI can print it out
            var component = page.Instance;
            var logsField = typeof(ActionHistory).GetField("_allLogs", BindingFlags.Instance | BindingFlags.NonPublic)!;
            logsField.SetValue(component, new List<ActionExecutionLog>());

            // 4. Simulate the Hub transmitting a real-time event
            var incomingLog = new ActionExecutionLog 
            { 
                Id = Guid.NewGuid(), 
                ActionName = "Real-time Bot Event Triggered!", 
                State = ActionExecutionState.Completed, 
                QueueName = "Default", 
                EnqueuedAt = DateTime.UtcNow 
            };

            // Execute the intercepted callback handler directly on bUnit's UI thread
            await _ctx!.Renderer.Dispatcher.InvokeAsync(async () =>
            {
                if (attachedActionLogUpdatedHandler != null)
                {
                    await attachedActionLogUpdatedHandler(incomingLog);
                }
            });

            // 5. Assert: Verify the new log entry rendered out directly into the page markup
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Real-time Bot Event Triggered!", page.Markup);
            });
        }

        [Fact]
        public async Task SignalR_WhenPaused_ShouldIncrementPendingCount()
        {
            _ctx = new BunitContext();
            _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            _ctx.Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
            _ctx.Services.AddSingleton<ISnackbar>(new Mock<ISnackbar>().Object);
            _ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            var mockExecutionLogger = new Mock<IActionExecutionLogger>();
            _ctx.Services.AddSingleton<IActionExecutionLogger>(mockExecutionLogger.Object);
            var mockQueueManager = new Mock<IQueueManager>();
            mockQueueManager.Setup(q => q.ExecutionLogger).Returns(mockExecutionLogger.Object);
            _ctx.Services.AddSingleton<IQueueManager>(mockQueueManager.Object);

            Func<ActionExecutionLog, Task>? attachedHandler = null;

            var mockHubConnection = new Mock<ISignalRHubConnection>();
            mockHubConnection
                .Setup(h => h.On<ActionExecutionLog>("ActionLogUpdated", It.IsAny<Func<ActionExecutionLog, Task>>()))
                .Callback<string, Func<ActionExecutionLog, Task>>((eventName, handler) => attachedHandler = handler);
            mockHubConnection.Setup(h => h.StartAsync()).Returns(Task.CompletedTask);

            var mockHubFactory = new Mock<ISignalRHubConnectionFactory>();
            mockHubFactory.Setup(f => f.CreateMainHubConnection(It.IsAny<Uri>())).Returns(mockHubConnection.Object);
            _ctx.Services.AddSingleton<ISignalRHubConnectionFactory>(mockHubFactory.Object);

            var page = _ctx.Render<ActionHistory>();
            var component = page.Instance;
            var logsField = typeof(ActionHistory).GetField("_allLogs", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var pausedField = typeof(ActionHistory).GetField("_updatesPaused", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var pendingField = typeof(ActionHistory).GetField("_pendingUpdatesCount", BindingFlags.Instance | BindingFlags.NonPublic)!;
            logsField.SetValue(component, new List<ActionExecutionLog>());
            pausedField.SetValue(component, true);
            pendingField.SetValue(component, 0);
            page.Render();

            var incomingLog = new ActionExecutionLog { Id = Guid.NewGuid(), ActionName = "NewPausedLog" };

            await _ctx!.Renderer.Dispatcher.InvokeAsync(async () =>
            {
                if (attachedHandler != null) await attachedHandler(incomingLog);
            });

            await page.WaitForAssertionAsync(() =>
            {
                var pending = (int)pendingField.GetValue(component)!;
                Assert.Equal(1, pending);
                Assert.DoesNotContain("NewPausedLog", page.Markup);
            });
        }

        [Fact]
        public async Task SignalR_WhenPaused_ExistingLog_ShouldNotIncrementPendingCount()
        {
            _ctx = new BunitContext();
            _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            _ctx.Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
            _ctx.Services.AddSingleton<ISnackbar>(new Mock<ISnackbar>().Object);
            _ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            var mockExecutionLogger = new Mock<IActionExecutionLogger>();
            _ctx.Services.AddSingleton<IActionExecutionLogger>(mockExecutionLogger.Object);
            var mockQueueManager = new Mock<IQueueManager>();
            mockQueueManager.Setup(q => q.ExecutionLogger).Returns(mockExecutionLogger.Object);
            _ctx.Services.AddSingleton<IQueueManager>(mockQueueManager.Object);

            Func<ActionExecutionLog, Task>? attachedHandler = null;

            var mockHubConnection = new Mock<ISignalRHubConnection>();
            mockHubConnection
                .Setup(h => h.On<ActionExecutionLog>("ActionLogUpdated", It.IsAny<Func<ActionExecutionLog, Task>>()))
                .Callback<string, Func<ActionExecutionLog, Task>>((eventName, handler) => attachedHandler = handler);
            mockHubConnection.Setup(h => h.StartAsync()).Returns(Task.CompletedTask);

            var mockHubFactory = new Mock<ISignalRHubConnectionFactory>();
            mockHubFactory.Setup(f => f.CreateMainHubConnection(It.IsAny<Uri>())).Returns(mockHubConnection.Object);
            _ctx.Services.AddSingleton<ISignalRHubConnectionFactory>(mockHubFactory.Object);

            var page = _ctx.Render<ActionHistory>();
            var component = page.Instance;
            var logsField = typeof(ActionHistory).GetField("_allLogs", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var pausedField = typeof(ActionHistory).GetField("_updatesPaused", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var pendingField = typeof(ActionHistory).GetField("_pendingUpdatesCount", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var existingLog = new ActionExecutionLog { Id = Guid.NewGuid(), ActionName = "ExistingLog" };
            logsField.SetValue(component, new List<ActionExecutionLog> { existingLog });
            pausedField.SetValue(component, true);
            pendingField.SetValue(component, 0);
            page.Render();

            var updateLog = new ActionExecutionLog { Id = existingLog.Id, ActionName = "UpdatedLog" };

            await _ctx!.Renderer.Dispatcher.InvokeAsync(async () =>
            {
                if (attachedHandler != null) await attachedHandler(updateLog);
            });

            await page.WaitForAssertionAsync(() =>
            {
                var pending = (int)pendingField.GetValue(component)!;
                Assert.Equal(0, pending);
                Assert.DoesNotContain("UpdatedLog", page.Markup);
            });
        }

        [Fact]
        public async Task SignalR_WhenNotPaused_ShouldUpdateExistingLog()
        {
            _ctx = new BunitContext();
            _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            _ctx.Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
            _ctx.Services.AddSingleton<ISnackbar>(new Mock<ISnackbar>().Object);
            _ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            var mockExecutionLogger = new Mock<IActionExecutionLogger>();
            _ctx.Services.AddSingleton<IActionExecutionLogger>(mockExecutionLogger.Object);
            var mockQueueManager = new Mock<IQueueManager>();
            mockQueueManager.Setup(q => q.ExecutionLogger).Returns(mockExecutionLogger.Object);
            _ctx.Services.AddSingleton<IQueueManager>(mockQueueManager.Object);

            Func<ActionExecutionLog, Task>? attachedHandler = null;

            var mockHubConnection = new Mock<ISignalRHubConnection>();
            mockHubConnection
                .Setup(h => h.On<ActionExecutionLog>("ActionLogUpdated", It.IsAny<Func<ActionExecutionLog, Task>>()))
                .Callback<string, Func<ActionExecutionLog, Task>>((eventName, handler) => attachedHandler = handler);
            mockHubConnection.Setup(h => h.StartAsync()).Returns(Task.CompletedTask);

            var mockHubFactory = new Mock<ISignalRHubConnectionFactory>();
            mockHubFactory.Setup(f => f.CreateMainHubConnection(It.IsAny<Uri>())).Returns(mockHubConnection.Object);
            _ctx.Services.AddSingleton<ISignalRHubConnectionFactory>(mockHubFactory.Object);

            var page = _ctx.Render<ActionHistory>();
            var component = page.Instance;
            var logsField = typeof(ActionHistory).GetField("_allLogs", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var existingLog = new ActionExecutionLog { Id = Guid.NewGuid(), ActionName = "OriginalName", State = ActionExecutionState.Pending };
            logsField.SetValue(component, new List<ActionExecutionLog> { existingLog });
            page.Render();

            var updatedLog = new ActionExecutionLog { Id = existingLog.Id, ActionName = "UpdatedName", State = ActionExecutionState.Completed };

            await _ctx!.Renderer.Dispatcher.InvokeAsync(async () =>
            {
                if (attachedHandler != null) await attachedHandler(updatedLog);
            });

            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("UpdatedName", page.Markup);
                Assert.DoesNotContain("OriginalName", page.Markup);
            });
        }

        [Fact]
        public async Task SignalR_StartFails_ShowsSnackbarWarning()
        {
            _ctx = new BunitContext();
            _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            _ctx.Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
            var mockSnackbar = new Mock<ISnackbar>();
            _ctx.Services.AddSingleton<ISnackbar>(mockSnackbar.Object);
            _ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            var mockExecutionLogger = new Mock<IActionExecutionLogger>();
            mockExecutionLogger.Setup(l => l.GetRecentLogs(It.IsAny<int>())).Returns(new List<ActionExecutionLog>());
            mockExecutionLogger.Setup(l => l.GetLogCount()).Returns(0);
            mockExecutionLogger.Setup(l => l.MaxLogCount()).Returns(1000);
            _ctx.Services.AddSingleton<IActionExecutionLogger>(mockExecutionLogger.Object);
            var mockQueueManager = new Mock<IQueueManager>();
            mockQueueManager.Setup(q => q.ExecutionLogger).Returns(mockExecutionLogger.Object);
            _ctx.Services.AddSingleton<IQueueManager>(mockQueueManager.Object);

            var mockHubConnection = new Mock<ISignalRHubConnection>();
            mockHubConnection.Setup(h => h.StartAsync()).Throws(new InvalidOperationException("Connection refused"));
            mockHubConnection.Setup(h => h.On<ActionExecutionLog>(It.IsAny<string>(), It.IsAny<Func<ActionExecutionLog, Task>>()));

            var mockHubFactory = new Mock<ISignalRHubConnectionFactory>();
            mockHubFactory.Setup(f => f.CreateMainHubConnection(It.IsAny<Uri>())).Returns(mockHubConnection.Object);
            _ctx.Services.AddSingleton<ISignalRHubConnectionFactory>(mockHubFactory.Object);

            var page = _ctx.Render<ActionHistory>();
            await page.WaitForAssertionAsync(() =>
            {
                mockSnackbar.Verify(s => s.Add(It.IsAny<string>(), Severity.Warning), Times.Once);
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
