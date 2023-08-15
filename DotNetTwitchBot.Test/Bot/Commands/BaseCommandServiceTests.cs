using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using NSubstitute;
using Xunit;

namespace DotNetTwitchBot.Tests.Bot.Commands
{
    public class BaseCommandServiceTests
    {
        [Fact]
        public async Task SendChatMessage_CallsServiceBackbone_SendChatMessage()
        {
            // Arrange
            var serviceBackboneSubstitute = Substitute.For<IServiceBackbone>();
            var commandHandlerSubstitute = Substitute.For<ICommandHandler>();

            var baseCommandService = new TestCommandService(serviceBackboneSubstitute, commandHandlerSubstitute);

            // Act
            await baseCommandService.SendChatMessage("Test Message");

            // Assert
            await serviceBackboneSubstitute.Received(1).SendChatMessage("Test Message");
        }

        [Fact]
        public async Task RegisterDefaultCommand_ValidCommand_RegistersDefaultCommand()
        {
            // Arrange
            var serviceBackboneSubstitute = Substitute.For<IServiceBackbone>();
            var commandHandlerSubstitute = Substitute.For<ICommandHandler>();
            var defaultCommandSubstitute = new DefaultCommand { CommandName = "testCommand" };

            commandHandlerSubstitute.GetDefaultCommandFromDb("testCommand").Returns(defaultCommandSubstitute);

            var baseCommandService = new TestCommandService(serviceBackboneSubstitute, commandHandlerSubstitute);

            // Act
            var result = await baseCommandService.RegisterDefaultCommand(defaultCommandSubstitute);

            // Assert
            Assert.Equal(defaultCommandSubstitute, result);
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            await commandHandlerSubstitute.DidNotReceiveWithAnyArgs().AddDefaultCommand(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        [Fact]
        public async Task RegisterDefaultCommand_NewCommand_RegistersAndAddsDefaultCommand()
        {
            // Arrange
            var serviceBackboneSubstitute = Substitute.For<IServiceBackbone>();
            var commandHandlerSubstitute = Substitute.For<ICommandHandler>();
            var defaultCommandSubstitute = new DefaultCommand { CommandName = "testCommand" };

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            commandHandlerSubstitute.GetDefaultCommandFromDb("testCommand").Returns((DefaultCommand)null);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            commandHandlerSubstitute.AddDefaultCommand(defaultCommandSubstitute).Returns(defaultCommandSubstitute);

            var baseCommandService = new TestCommandService(serviceBackboneSubstitute, commandHandlerSubstitute);

            // Act
            var result = await baseCommandService.RegisterDefaultCommand(defaultCommandSubstitute);

            // Assert
            Assert.Equal(defaultCommandSubstitute, result);
            await commandHandlerSubstitute.Received(1).AddDefaultCommand(defaultCommandSubstitute);
        }

        // Add more tests for other methods
    }

    internal class TestCommandService : BaseCommandService
    {
        public TestCommandService(IServiceBackbone serviceBackbone, ICommandHandler commandHandler)
            : base(serviceBackbone, commandHandler)
        {
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.FromResult(true);
        }

        public override Task Register()
        {
            return Task.CompletedTask;
        }
    }
}
