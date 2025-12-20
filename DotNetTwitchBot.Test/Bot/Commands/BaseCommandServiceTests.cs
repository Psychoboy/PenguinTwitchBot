using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Commands;
using MediatR;
using NSubstitute;

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
            var mediatorSubstitute = Substitute.For<IMediator>();

            var baseCommandService = new TestCommandService(serviceBackboneSubstitute, commandHandlerSubstitute, mediatorSubstitute);

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
            var mediatorSubstitute = Substitute.For<IMediator>();
            var defaultCommandSubstitute = new DefaultCommand { CommandName = "testCommand" };

            commandHandlerSubstitute.GetDefaultCommandFromDb("testCommand").Returns(defaultCommandSubstitute);

            var baseCommandService = new TestCommandService(serviceBackboneSubstitute, commandHandlerSubstitute, mediatorSubstitute);

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
            var mediatorSubstitute = Substitute.For<IMediator>();
            var defaultCommandSubstitute = new DefaultCommand { CommandName = "testCommand" };

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            commandHandlerSubstitute.GetDefaultCommandFromDb("testCommand").Returns((DefaultCommand)null);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            commandHandlerSubstitute.AddDefaultCommand(defaultCommandSubstitute).Returns(defaultCommandSubstitute);

            var baseCommandService = new TestCommandService(serviceBackboneSubstitute, commandHandlerSubstitute, mediatorSubstitute);

            // Act
            var result = await baseCommandService.RegisterDefaultCommand(defaultCommandSubstitute);

            // Assert
            Assert.Equal(defaultCommandSubstitute, result);
            await commandHandlerSubstitute.Received(1).AddDefaultCommand(defaultCommandSubstitute);
        }

        [Fact]
        public async Task ShouldSendChatMessage()
        {
            // Arrange
            var serviceBackboneSubstitute = Substitute.For<IServiceBackbone>();
            var commandHandlerSubstitute = Substitute.For<ICommandHandler>();
            var mediatorSubstitute = Substitute.For<IMediator>();

            var baseCommandService = new TestCommandService(serviceBackboneSubstitute, commandHandlerSubstitute, mediatorSubstitute);

            // Act
            await baseCommandService.SendChatMessage("Test Message");

            // Assert
            await serviceBackboneSubstitute.Received(1).SendChatMessage("Test Message");
        }

        [Fact]
        public async Task ShouldSendChatMessage_WithName()
        {
            // Arrange
            var serviceBackboneSubstitute = Substitute.For<IServiceBackbone>();
            var commandHandlerSubstitute = Substitute.For<ICommandHandler>();
            var mediatorSubstitute = Substitute.For<IMediator>();

            var baseCommandService = new TestCommandService(serviceBackboneSubstitute, commandHandlerSubstitute, mediatorSubstitute);

            // Act
            await baseCommandService.SendChatMessage("TestName", "Test Message");

            // Assert
            await serviceBackboneSubstitute.Received(1).SendChatMessage("TestName", "Test Message");
        }
    }

    internal class TestCommandService : BaseCommandService
    {
        public TestCommandService(IServiceBackbone serviceBackbone, ICommandHandler commandHandler, IMediator mediator)
            : base(serviceBackbone, commandHandler, "Roulette", mediator)
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
