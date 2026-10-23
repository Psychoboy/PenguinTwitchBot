using Xunit;
using Bunit;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Pages.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Bot.Models.Timers;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;
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

            var configuration = new ConfigurationBuilder().Build();
            _ctx.Services.AddSingleton<IConfiguration>(configuration);

            var featureCoordinator = Substitute.For<IFeatureRuntimeCoordinator>();
            featureCoordinator.IsEnabled(Arg.Any<string>()).Returns(true);
            _ctx.Services.AddSingleton(featureCoordinator);

            var commandService = Substitute.For<IActionCommandService>();
            commandService.GetAllAsync().Returns(Task.FromResult(new List<ActionCommand>()));
            _ctx.Services.AddSingleton(commandService);

            var keywordService = Substitute.For<IActionKeywordService>();
            keywordService.GetAllAsync().Returns(Task.FromResult(new List<ActionKeyword>()));
            _ctx.Services.AddSingleton(keywordService);

            var timersService = Substitute.For<PenguinTwitchBot.Bot.Commands.Misc.IAutoTimers>();
            timersService.GetTimerGroupsAsync().Returns(Task.FromResult(new List<TimerGroup>()));
            _ctx.Services.AddSingleton(timersService);

            var commandHandler = Substitute.For<ICommandHandler>();
            commandHandler.GetDefaultCommandsFromDb().Returns(Task.FromResult(new List<DefaultCommand>
            {
                new() { Id = 1, CommandName = "gamble", ModuleName = "Gamble" },
                new() { Id = 2, CommandName = "defuse", ModuleName = "Defuse" },
                new() { Id = 3, CommandName = "roll", ModuleName = "Roll" },
                new() { Id = 4, CommandName = "slot", ModuleName = "Slots" },
                new() { Id = 5, CommandName = "steal", ModuleName = "Steal" },
                new() { Id = 6, CommandName = "heist", ModuleName = "Heist" },
                new() { Id = 7, CommandName = "death", ModuleName = "DeathCounter" },
                new() { Id = 8, CommandName = "spinwheel", ModuleName = "WheeledGame" },
                new() { Id = 9, CommandName = "drop", ModuleName = "DropGames" },
                new() { Id = 10, CommandName = "plinko", ModuleName = "DropGames" }
            }));
            _ctx.Services.AddSingleton(commandHandler);

            var fishingService = Substitute.For<IFishingService>();
            fishingService.GetAllFishTypes().Returns(Task.FromResult(new List<FishType>()));
            _ctx.Services.AddSingleton(fishingService);

            var twitchService = Substitute.For<ITwitchService>();
            twitchService.GetChannelPointRewards().Returns(Task.FromResult(System.Linq.Enumerable.Empty<ChannelPointReward>()));
            _ctx.Services.AddSingleton(twitchService);
        }

        [Fact]
        public async Task Dialog_Renders_WithoutError()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            Assert.Contains("Select Trigger Type", dialogProvider.Markup);
            Assert.Contains("Choose a category or search for a trigger", dialogProvider.Markup);
        }

        [Fact]
        public async Task MainList_ContainsExpectedDomainCategories()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.Contains("Twitch &amp; Stream", markup);
                Assert.Contains("Minigames", markup);
                Assert.Contains("Gambling &amp; Points", markup);
                Assert.Contains("Fishing", markup);
                Assert.Contains("Chat &amp; Commands", markup);
                Assert.Contains("Counters &amp; Utilities", markup);
                Assert.Contains("Timers &amp; Schedules", markup);
                Assert.Contains("Media &amp; Audio", markup);
                Assert.Contains("General &amp; Manual", markup);
            });
        }

        [Fact]
        public async Task ClickMinigamesCategory_ShowsMinigameTriggers()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var minigamesCategory = dialogProvider.Find(".trigger-category-minigames");
            minigamesCategory.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.Contains("Minigames Triggers", markup);
                Assert.Contains("Parachute Drop - Bullseye", markup);
                Assert.Contains("Plinko - Win", markup);
                Assert.Contains("Defuse - Success", markup);
                Assert.Contains("Bank Heist - Started", markup);
            });
        }

        [Fact]
        public async Task ClickGamblingCategory_ShowsGamblingTriggers()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var gamblingCategory = dialogProvider.Find(".trigger-category-gambling");
            gamblingCategory.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.Contains("Gambling &amp; Points Triggers", markup);
                Assert.Contains("Gamble - Jackpot Win", markup);
                Assert.Contains("Slots - 3 of a Kind", markup);
                Assert.Contains("Dice Roll - Doubles", markup);
                Assert.Contains("Steal - Success", markup);
            });
        }

        [Fact]
        public async Task ClickCountersCategory_ShowsDeathCounterTriggers()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var countersCategory = dialogProvider.Find(".trigger-category-counters");
            countersCategory.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.Contains("Counters &amp; Utilities Triggers", markup);
                Assert.Contains("Death Counter (!death) - Added (+)", markup);
                Assert.Contains("Death Counter (!death) - Reset", markup);
            });
        }

        [Fact]
        public async Task ClickTrigger_FromCategory_ShowsConfiguration()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var gamblingCategory = dialogProvider.Find(".trigger-category-gambling");
            gamblingCategory.Click();

            var gambleWin = dialogProvider.Find(".trigger-gamble-win");
            gambleWin.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.Contains("Configure Gamble - Win", markup);
                Assert.Contains("Select Default Command", markup);
                Assert.Equal("Gamble - Win", dialogProvider.FindComponents<MudTextField<string>>()
                    .Single(component => component.Instance.Label == "Trigger Name").Find("input").GetAttribute("value"));
            });
        }

        [Fact]
        public async Task BackButton_FromTriggerList_ReturnsToCategories()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var minigamesCategory = dialogProvider.Find(".trigger-category-minigames");
            minigamesCategory.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Minigames Triggers", dialogProvider.Markup);
            });

            var backButton = dialogProvider.Find("button:contains('Back')");
            backButton.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Select Trigger Type", dialogProvider.Markup);
                Assert.DoesNotContain("Minigames Triggers", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task BackButton_FromConfigure_ReturnsToCategoryTriggerList()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var gamblingCategory = dialogProvider.Find(".trigger-category-gambling");
            gamblingCategory.Click();

            var gambleWin = dialogProvider.Find(".trigger-gamble-win");
            gambleWin.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Configure Gamble - Win", dialogProvider.Markup);
            });

            var backButton = dialogProvider.Find("button:contains('Back')");
            backButton.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Gambling &amp; Points Triggers", dialogProvider.Markup);
                Assert.DoesNotContain("Configure Gamble - Win", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task Search_ShowsMatchingTriggersDirectly()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var searchInput = dialogProvider.Find("input");
            searchInput.Input("gamble");

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.Contains("Gamble - Jackpot Win", markup);
                Assert.Contains("Gamble - Win", markup);
                Assert.Contains("Gamble - Lose", markup);
            });

            var jackpotItem = dialogProvider.Find(".trigger-gamble-jackpot");
            jackpotItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Configure Gamble - Jackpot Win", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task Search_TwitchRaid_ShowsChannelRaid()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var searchInput = dialogProvider.Find("input");
            searchInput.Input("raid");

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.Contains("Channel Raid", markup);
            });

            var raidItem = dialogProvider.Find(".trigger-twitch-channelraid");
            raidItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Configure Channel Raid", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task BackButton_FromSearchConfigure_ReturnsToSearchResults()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            var searchInput = dialogProvider.Find("input");
            searchInput.Input("plinko");

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Plinko - Win", dialogProvider.Markup);
            });

            var plinkoItem = dialogProvider.Find(".trigger-plinko-win");
            plinkoItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Configure Plinko - Win", dialogProvider.Markup);
            });

            var backButton = dialogProvider.Find("button:contains('Back')");
            backButton.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Plinko - Win", dialogProvider.Markup);
                Assert.DoesNotContain("Configure Plinko - Win", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task Cancel_ClosesDialog()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Test Title"));

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Select Trigger Type", dialogProvider.Markup);
            });

            var cancelButton = dialogProvider.Find("button:contains('Cancel')");
            cancelButton.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.DoesNotContain("Select Trigger Type", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task EditMode_OpensDirectlyInConfigure()
        {
            SetupContext();
            var existingTrigger = new TriggerType
            {
                Id = 42,
                Name = "gamble_Win",
                Type = TriggerTypes.DefaultCommand,
                DefaultCommandId = 1,
                Configuration = "{\"DefaultCommandName\":\"gamble\",\"EventType\":\"Gamble.Win\"}"
            };

            var parameters = new DialogParameters<AddTriggerDialog>
            {
                { x => x.ExistingTrigger, existingTrigger }
            };

            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddTriggerDialog>("Edit Trigger", parameters));

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.Contains("Configure Gamble - Win", markup);
                Assert.Contains("Update Trigger", markup);
                Assert.Empty(dialogProvider.FindAll(".btn-back"));
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
