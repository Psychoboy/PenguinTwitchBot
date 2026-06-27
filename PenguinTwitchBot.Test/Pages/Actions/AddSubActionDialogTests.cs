using Xunit;
using Bunit;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using PenguinTwitchBot.Pages.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Test.Pages.Actions
{
    public class AddSubActionDialogSmokeTests : IAsyncLifetime
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

        [Fact]
        public async Task Dialog_Renders_WithoutError()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));
            Assert.Contains("Select SubAction Type", dialogProvider.Markup);
        }

        [Fact]
        public async Task ClickDelay_ShowsConfiguration()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            var delayItem = dialogProvider.Find(".subaction-delay");
            delayItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Configure Delay", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task ClickSendMessage_ShowsConfiguration()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            var sendMessageItem = dialogProvider.Find(".subaction-sendmessage");
            sendMessageItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Configure Send Message", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task ClickObsCategory_ShowsObsTypeList()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            var obsItem = dialogProvider.Find(".subaction-obs-category");
            obsItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("OBS Actions", dialogProvider.Markup);
                Assert.Contains("Set Scene", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task ClickObsSetScene_FromObsCategory_ShowsConfiguration()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            var obsItem = dialogProvider.Find(".subaction-obs-category");
            obsItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Select an OBS action type", dialogProvider.Markup);
            });

            var setSceneItem = dialogProvider.Find(".subaction-obssetscene");
            setSceneItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Configure OBS - Set Scene", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task BackButton_FromObsTypeList_ReturnsToTypeSelection()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            var obsItem = dialogProvider.Find(".subaction-obs-category");
            obsItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("OBS Actions", dialogProvider.Markup);
            });

            var backButton = dialogProvider.Find("button:contains('Back')");
            backButton.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Select SubAction Type", dialogProvider.Markup);
                Assert.DoesNotContain("OBS Actions", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task BackButton_FromConfigure_ReturnsToTypeSelection()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            var delayItem = dialogProvider.Find(".subaction-delay");
            delayItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Configure Delay", dialogProvider.Markup);
            });

            var backButton = dialogProvider.Find("button:contains('Back')");
            backButton.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Select SubAction Type", dialogProvider.Markup);
                Assert.DoesNotContain("Configure Delay", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task BackButton_FromObsConfigure_ReturnsToObsTypeList()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            var obsItem = dialogProvider.Find(".subaction-obs-category");
            obsItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("OBS Actions", dialogProvider.Markup);
            });

            var setSceneItem = dialogProvider.Find(".subaction-obssetscene");
            setSceneItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Configure OBS - Set Scene", dialogProvider.Markup);
            });

            var backButton = dialogProvider.Find("button:contains('Back')");
            backButton.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("OBS Actions", dialogProvider.Markup);
                Assert.DoesNotContain("Configure OBS - Set Scene", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task Cancel_ClosesDialog()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.Contains("Select SubAction Type", dialogProvider.Markup);
            });

            var cancelButton = dialogProvider.Find("button:contains('Cancel')");
            cancelButton.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                Assert.DoesNotContain("Select SubAction Type", dialogProvider.Markup);
            });
        }

        [Fact]
        public async Task NonSearching_MainList_HidesObsTypesButShowsCategory()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.Contains("OBS", markup);
                Assert.DoesNotContain("Set Scene", markup);
                Assert.DoesNotContain("Configure OBS -", markup);
            });
        }

        [Fact]
        public async Task ObsItems_ShowDisplayNameWithoutPrefix()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            var obsItem = dialogProvider.Find(".subaction-obs-category");
            obsItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.Contains("Set Scene", markup);
                Assert.DoesNotContain("OBS - Set Scene", markup);
            });
        }

        [Fact]
        public async Task SubmitButton_HiddenInSelectTypeStep()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.DoesNotContain("Add SubAction", markup);
            });
        }

        [Fact]
        public async Task SubmitButton_HiddenInSelectObsTypeStep()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            var obsItem = dialogProvider.Find(".subaction-obs-category");
            obsItem.Click();

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.DoesNotContain("Add SubAction", markup);
            });
        }

        [Fact]
        public async Task MainList_ContainsExpectedNonObsTypes()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();
            await _ctx.Renderer.Dispatcher.InvokeAsync(() => dialogService.ShowAsync<AddSubActionDialog>("Test Title"));

            dialogProvider.WaitForAssertion(() =>
            {
                var markup = dialogProvider.Markup;
                Assert.Contains("Delay", markup);
                Assert.Contains("Send Message", markup);
                Assert.Contains("Alert", markup);
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
