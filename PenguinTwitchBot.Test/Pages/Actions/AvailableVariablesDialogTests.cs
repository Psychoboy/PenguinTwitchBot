using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.Variables;
using PenguinTwitchBot.Pages.Actions;
using PenguinTwitchBot.Pages.Components;
using Xunit;

namespace PenguinTwitchBot.Test.Pages.Actions
{
    public class AvailableVariablesDialogTests : IAsyncLifetime
    {
        private BunitContext? _ctx;

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            if (_ctx != null)
            {
                await _ctx.DisposeAsync();
            }
        }

        private void SetupContext(List<ActionVariableInfo>? sampleVariables = null)
        {
            _ctx = new BunitContext();
            _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            _ctx.Services.AddMudServices(options =>
            {
                options.PopoverOptions.CheckForPopoverProvider = false;
            });

            var configuration = new ConfigurationBuilder().Build();
            _ctx.Services.AddSingleton<IConfiguration>(configuration);

            var variableResolver = Substitute.For<IActionVariableResolver>();
            var variablesToReturn = sampleVariables ?? new List<ActionVariableInfo>
            {
                new("user", "User who initiated the action", "System Global", "Globals", "Streamer"),
                new("TargetUser", "Target user specified", "Caller Action", "Caller Actions", "TargetViewer"),
                new("Args", "Arguments passed", "Chat Command", "Triggers", "arg1 arg2")
            };

            variableResolver.ResolveVariablesAsync(
                Arg.Any<int?>(),
                Arg.Any<IEnumerable<PenguinTwitchBot.Database.Bot.Models.Actions.Triggers.TriggerType>?>(),
                Arg.Any<IEnumerable<PenguinTwitchBot.Database.Bot.Actions.SubActions.Types.SubActionType>?>(),
                Arg.Any<bool>(),
                Arg.Any<HashSet<int>?>())
                .Returns(Task.FromResult(variablesToReturn));

            _ctx.Services.AddSingleton(variableResolver);
        }

        [Fact]
        public async Task AvailableVariablesDialog_Renders_WithoutError()
        {
            SetupContext();
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();

            var parameters = new DialogParameters<AvailableVariablesDialog>
            {
                { x => x.ActionId, 1 },
                { x => x.ActionName, "Test Action" }
            };

            await _ctx.Renderer.Dispatcher.InvokeAsync(() => 
                dialogService.ShowAsync<AvailableVariablesDialog>("Available Variables (Test Action)", parameters));

            Assert.Contains("Available Variables", dialogProvider.Markup);
            Assert.Contains("Test Action", dialogProvider.Markup);
            Assert.Contains("%user%", dialogProvider.Markup);
            Assert.Contains("%Args%", dialogProvider.Markup);
        }

        [Fact]
        public void ActionVariablesReference_Renders_VariableTokens_And_Categories()
        {
            SetupContext();
            var variables = new List<ActionVariableInfo>
            {
                new("bot", "Bot info", "System Global", "System", "TheBot"),
                new("CustomVar", "User set variable", "Step 1", "Previous Steps", "123")
            };

            var cut = _ctx!.Render<ActionVariablesReference>(p => p
                .Add(x => x.Variables, variables));

            Assert.Contains("%bot%", cut.Markup);
            Assert.Contains("%CustomVar%", cut.Markup);
            Assert.Contains("System", cut.Markup);
            Assert.Contains("Previous Steps", cut.Markup);
        }

        [Fact]
        public async Task AvailableVariablesDialog_EmptyVariables_Renders_Gracefully()
        {
            SetupContext(new List<ActionVariableInfo>());
            var dialogProvider = _ctx!.Render<MudDialogProvider>();
            var dialogService = _ctx.Services.GetRequiredService<IDialogService>();

            await _ctx.Renderer.Dispatcher.InvokeAsync(() => 
                dialogService.ShowAsync<AvailableVariablesDialog>("Available Variables"));

            Assert.Contains("Available Variables", dialogProvider.Markup);
            Assert.Contains("No variables match the selected filter", dialogProvider.Markup);
        }
    }
}
