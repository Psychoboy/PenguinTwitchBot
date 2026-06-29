using Bunit;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Moq;
using NSubstitute;
using System.Collections.Concurrent;
using PenguinTwitchBot.Pages.Commands;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Application.Notifications;

namespace PenguinTwitchBot.Test.Pages.Commands
{
    public class AudioCommandsModernTests : IAsyncLifetime
    {
        private BunitContext? _ctx;
        private global::PenguinTwitchBot.Bot.Commands.AudioCommand.AudioCommands? _audioCommandService;

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
            
            var mockCommandHelper = Substitute.For<ICommandHelper>();
            _ctx.Services.AddSingleton<ICommandHelper>(mockCommandHelper);
        }

        private static List<AudioCommand> CreateTestCommands()
        {
            return
            [
                new AudioCommand
                {
                    Id = 1,
                    CommandName = "test",
                    Category = "Test Category",
                    Description = "A test command",
                    AudioFile = "test.mp3",
                    Disabled = false,
                    UserCooldown = 5,
                    GlobalCooldown = 10,
                    Cost = 0,
                    MinimumRank = Rank.Viewer,
                    SayCooldown = true,
                    SourceOnly = true
                },
                new AudioCommand
                {
                    Id = 2,
                    CommandName = "hello",
                    Category = "Greetings",
                    Description = "Say hello",
                    AudioFile = "hello.mp3",
                    Disabled = true,
                    UserCooldown = 0,
                    GlobalCooldown = 0,
                    Cost = 0,
                    MinimumRank = Rank.Viewer,
                    SayCooldown = false
                }
            ];
        }

        private void SetupServices(List<AudioCommand>? commands = null)
        {
            commands ??= CreateTestCommands();

            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var scope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var dbContext = Substitute.For<PenguinTwitchBot.Database.Repository.IUnitOfWork>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(PenguinTwitchBot.Database.Repository.IUnitOfWork)).Returns(dbContext);
            dbContext.AudioCommands.GetAllAsync().Returns(Task.FromResult((IEnumerable<AudioCommand>)commands));
            
            _audioCommandService = new global::PenguinTwitchBot.Bot.Commands.AudioCommand.AudioCommands(
                Substitute.For<IPenguinDispatcher>(),
                scopeFactory,
                Substitute.For<ILogger<global::PenguinTwitchBot.Bot.Commands.AudioCommand.AudioCommands>>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<ILanguage>(),
                Substitute.For<ICommandHandler>());

            var mockDialogService = new Mock<IDialogService>();
            mockDialogService.Setup(s => s.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DialogOptions?>()))
                .Returns(Task.FromResult<bool?>(true));

            var mockSnackbar = new Mock<ISnackbar>();

            _ctx!.Services.AddSingleton(_audioCommandService);
            _ctx.Services.AddSingleton<IDialogService>(mockDialogService.Object);
            _ctx.Services.AddSingleton<ISnackbar>(mockSnackbar.Object);
        }

        [Fact]
        public async Task Page_Renders_WithCommands()
        {
            SetupContext();
            SetupServices();
            PopulateCommandsField(CreateTestCommands());

            var page = _ctx!.Render<AudioCommandsModern>();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Audio Commands", page.Markup);
            });
        }

        [Fact]
        public async Task EmptyState_ShowsNoCommands()
        {
            SetupContext();
            SetupServices(new List<AudioCommand>());
            
            var page = _ctx!.Render<AudioCommandsModern>();
            page.WaitForAssertion(() =>
            {
                Assert.Contains("No audio commands found", page.Markup);
            });
        }

        [Fact]
        public async Task Page_ShowsCommandCount()
        {
            SetupContext();
            SetupServices();
            PopulateCommandsField(CreateTestCommands());

            var page = _ctx!.Render<AudioCommandsModern>();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("Audio Commands (2)", page.Markup);
            });
        }

        [Fact]
        public async Task Page_DisplaysCommandNames()
        {
            SetupContext();
            var commands = CreateTestCommands();
            SetupServices(commands);
            PopulateCommandsField(commands);

            var page = _ctx!.Render<AudioCommandsModern>();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("!test", page.Markup);
                Assert.Contains("!hello", page.Markup);
            });
        }

        [Fact]
        public async Task Page_DisplaysAudioFileNames()
        {
            SetupContext();
            var commands = CreateTestCommands();
            SetupServices(commands);
            PopulateCommandsField(commands);

            var page = _ctx!.Render<AudioCommandsModern>();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("test.mp3", page.Markup);
                Assert.Contains("hello.mp3", page.Markup);
            });
        }

        [Fact]
        public async Task Loading_ShowsInitialState()
        {
            SetupContext();
            var commands = CreateTestCommands();
            SetupServices(commands);
            PopulateCommandsField(commands);

            var page = _ctx!.Render<AudioCommandsModern>();
            page.WaitForAssertion(() =>
            {
                Assert.Contains("Audio Commands", page.Markup);
            });
        }

        [Fact]
        public async Task DisabledCommand_ShowsErrorChip()
        {
            SetupContext();
            SetupServices();
            PopulateCommandsField(CreateTestCommands());

            var page = _ctx!.Render<AudioCommandsModern>();
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
            PopulateCommandsField(CreateTestCommands());

            var page = _ctx!.Render<AudioCommandsModern>();
            await page.WaitForAssertionAsync(() =>
            {
                Assert.Contains("!test", page.Markup);
                Assert.Contains("Enabled", page.Markup);
            });
        }

        private void PopulateCommandsField(List<AudioCommand> commands)
        {
            var commandsField = typeof(global::PenguinTwitchBot.Bot.Commands.AudioCommand.AudioCommands)
                .GetField("Commands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dict = new ConcurrentDictionary<string, AudioCommand>();
            foreach (var cmd in commands)
            {
                dict[cmd.CommandName] = cmd;
            }
            commandsField!.SetValue(_audioCommandService, dict);
        }

        public async Task DisposeAsync()
        {
            if (_ctx != null)
            {
                await _ctx.DisposeAsync();
            }
        }
    }
}