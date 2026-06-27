using Xunit;
using Bunit;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Moq;
using PenguinTwitchBot.Pages.Actions;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Timers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Test.Pages.Actions
{
    public class AddTriggerDialogSmokeTests : IAsyncLifetime
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

            var mockCommandService = new Mock<IActionCommandService>();
            mockCommandService.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<ActionCommand>());
            _ctx.Services.AddSingleton<IActionCommandService>(mockCommandService.Object);

            var mockActionKeyService = new Mock<IActionKeywordService>();
            mockActionKeyService.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<ActionKeyword>());
            _ctx.Services.AddSingleton<IActionKeywordService>(mockActionKeyService.Object);
            
            var mockAutoTimerService = new Mock<IAutoTimers>();
            mockAutoTimerService.Setup(x => x.GetTimerGroupsAsync()).ReturnsAsync(new List<TimerGroup>());
            _ctx.Services.AddSingleton<IAutoTimers>(mockAutoTimerService.Object);
            
            var mockCommandHandler = new Mock<ICommandHandler>();
            mockCommandHandler.Setup(x => x.GetDefaultCommandsFromDb()).ReturnsAsync(new List<DefaultCommand>());
            _ctx.Services.AddSingleton<ICommandHandler>(mockCommandHandler.Object);

            var mockPointsSystem = new Mock<IPointsSystem>();
            _ctx.Services.AddSingleton<IPointsSystem>(mockPointsSystem.Object);
            
            var mockTwitchService = new Mock<ITwitchService>();
            mockTwitchService.Setup(x => x.GetChannelPointRewards()).ReturnsAsync(new List<PenguinTwitchBot.TwitchApi.Models.ChannelPoints.ChannelPointReward>());
            _ctx.Services.AddSingleton<ITwitchService>(mockTwitchService.Object);
            
            var configuration = new ConfigurationBuilder().Build();
            _ctx.Services.AddSingleton<IConfiguration>(configuration);
        }

        [Fact]
        public async Task Dialog_Renders_WithoutError()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));
            Assert.Contains("Select Trigger Type", dialogProvider.Markup);
        }

        [Fact]
        public async Task ClickTriggerCommand_ShowsCommandConfiguration()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var commandItem = dialogProvider.Find(".trigger-command");
            commandItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var finalMarkup = dialogProvider.Markup;
                Assert.Contains("Configure Command Trigger", finalMarkup);
            });
        }

        [Fact]
        public async Task ClickTriggerTimer_ShowsTimerConfiguration()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var timerItem = dialogProvider.Find(".trigger-timer");
            timerItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var finalMarkup = dialogProvider.Markup;
                Assert.Contains("Configure Timer Trigger", finalMarkup);
            });
        }

        [Fact]
        public async Task ClickTriggerKeyword_ShowsKeywordConfiguration()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var keywordItem = dialogProvider.Find(".trigger-keyword");
            keywordItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var finalMarkup = dialogProvider.Markup;
                Assert.Contains("Configure Keyword Trigger", finalMarkup);
            });
        }

        [Fact]
        public async Task ClickTriggerDefaultCommand_ShowsDefaultCommandConfiguration()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var defaultCommandItem = dialogProvider.Find(".trigger-defaultcommand");
            defaultCommandItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var finalMarkup = dialogProvider.Markup;
                Assert.Contains("Configure Default Command Trigger", finalMarkup);
            });
        }

        [Fact]
        public async Task ClickTriggerManual_ShowsManualConfiguration()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var manualItem = dialogProvider.Find(".trigger-manual");
            manualItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var finalMarkup = dialogProvider.Markup;
                Assert.Contains("Configure Manual Trigger", finalMarkup);
            });
        }

        [Fact]
        public async Task ClickTriggerTwitchEvent_ShowsTwitchEventConfiguration()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var twitchEventItem = dialogProvider.Find(".trigger-twitchevent");
            twitchEventItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var finalMarkup = dialogProvider.Markup;
                Assert.Contains("Configure Twitch Event Trigger", finalMarkup);
            });
        }

        [Fact]
        public async Task BackButton_ReturnsToTypeSelection()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var commandItem = dialogProvider.Find(".trigger-command");
            commandItem.Click();

            var backButton = dialogProvider.Find(".btn-back");
            backButton.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var finalMarkup = dialogProvider.Markup;
                Assert.Contains("Select Trigger Type", finalMarkup);
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
