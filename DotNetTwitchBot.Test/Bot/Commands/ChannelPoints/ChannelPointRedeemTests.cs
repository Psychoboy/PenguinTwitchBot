using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.ChannelPoints;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.CustomMiddleware;
using DotNetTwitchBot.Models;
using DotNetTwitchBot.Repository;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using MockQueryable;

namespace DotNetTwitchBot.Tests.Bot.Commands.ChannelPoints
{
    public class ChannelPointRedeemTests
    {
        private readonly ILogger<DotNetTwitchBot.Bot.Commands.ChannelPoints.ChannelPointRedeem> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly ICommandHandler _commandHandler;
        private readonly IMediator _mediatorSubstitute;
        private readonly DotNetTwitchBot.Bot.Commands.ChannelPoints.ChannelPointRedeem _channelPointRedeem;
        private readonly IUnitOfWork _unitOfWork;

        public ChannelPointRedeemTests()
        {
            _logger = Substitute.For<ILogger<DotNetTwitchBot.Bot.Commands.ChannelPoints.ChannelPointRedeem>>();
            _scopeFactory = Substitute.For<IServiceScopeFactory>();
            _serviceBackbone = Substitute.For<IServiceBackbone>();
            _commandHandler = Substitute.For<ICommandHandler>();
            _mediatorSubstitute = Substitute.For<IMediator>();

            var scope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            _unitOfWork = Substitute.For<IUnitOfWork>();

            _scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);

            _channelPointRedeem = new DotNetTwitchBot.Bot.Commands.ChannelPoints.ChannelPointRedeem(_logger, _scopeFactory, _serviceBackbone, _mediatorSubstitute, _commandHandler);
        }

        [Fact]
        public async Task AddRedeem_ShouldAddRedeem()
        {
            // Arrange
            var redeem = new DotNetTwitchBot.Bot.Models.ChannelPointRedeem
            {
                Name = "TestRedeem",
                Command = "TestCommand",
                ElevatedPermission = Rank.Viewer
            };

            // Act
            await _channelPointRedeem.AddRedeem(redeem);

            // Assert
            await _unitOfWork.ChannelPointRedeems.Received(1).AddAsync(redeem);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task DeleteRedeem_ShouldDeleteRedeem()
        {
            // Arrange
            var redeem = new DotNetTwitchBot.Bot.Models.ChannelPointRedeem
            {
                Name = "TestRedeem",
                Command = "TestCommand",
                ElevatedPermission = Rank.Viewer
            };

            // Act
            await _channelPointRedeem.DeleteRedeem(redeem);

            // Assert
            _unitOfWork.ChannelPointRedeems.Received(1).Remove(redeem);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GetRedeems_ShouldReturnListOfRedeems()
        {
            // Arrange
            var redeems = new List<DotNetTwitchBot.Bot.Models.ChannelPointRedeem>
            {
                new DotNetTwitchBot.Bot.Models.ChannelPointRedeem { Name = "TestRedeem1", Command = "TestCommand1", ElevatedPermission = Rank.Viewer },
                new DotNetTwitchBot.Bot.Models.ChannelPointRedeem { Name = "TestRedeem2", Command = "TestCommand2", ElevatedPermission = Rank.Moderator }
            };
            _unitOfWork.ChannelPointRedeems.GetAllAsync().Returns(redeems);

            // Act
            var result = await _channelPointRedeem.GetRedeems();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("TestRedeem1", result[0].Name);
            Assert.Equal("TestRedeem2", result[1].Name);
        }

        [Fact]
        public async Task OnChannelPointRedeem_ShouldExecuteRedeem()
        {
            // Arrange
            var redeem = new DotNetTwitchBot.Bot.Models.ChannelPointRedeem
            {
                Name = "TestRedeem",
                Command = "TestCommand (input)",
                ElevatedPermission = Rank.Viewer
            };

            var redeems = new List<DotNetTwitchBot.Bot.Models.ChannelPointRedeem> { redeem };
            var mockQueryable = redeems.BuildMockDbSet().AsQueryable();

            _unitOfWork.ChannelPointRedeems.Find(Arg.Any<System.Linq.Expressions.Expression<Func<DotNetTwitchBot.Bot.Models.ChannelPointRedeem, bool>>>())
                .Returns(mockQueryable);

            var eventArgs = new ChannelPointRedeemEventArgs
            {
                UserId = "123",
                Sender = "TestUser",
                Title = "TestRedeem",
                UserInput = "TestInput"
            };

            await _channelPointRedeem.StartAsync(default);
            // Act
            _serviceBackbone.ChannelPointRedeemEvent += Raise.Event<AsyncEventHandler<ChannelPointRedeemEventArgs>>(this, eventArgs);

            // Assert
            await _serviceBackbone.Received(1).RunCommand(Arg.Is<CommandEventArgs>(cmdArgs =>
                cmdArgs.UserId == "123" &&
                cmdArgs.Command == "TestCommand" &&
                cmdArgs.Arg == "TestInput" &&
                cmdArgs.Name == "TestUser"
            ));
        }

        [Fact]
        public async Task StartAsync_ShouldSubscribeToChannelPointRedeemEvent()
        {
            // Act
            await _channelPointRedeem.StartAsync(CancellationToken.None);

            // Assert
            _serviceBackbone.Received(1).ChannelPointRedeemEvent += Arg.Any<AsyncEventHandler<ChannelPointRedeemEventArgs>>();
        }

        [Fact]
        public async Task StopAsync_ShouldUnsubscribeFromChannelPointRedeemEvent()
        {
            // Act
            await _channelPointRedeem.StopAsync(CancellationToken.None);

            // Assert
            _serviceBackbone.Received(1).ChannelPointRedeemEvent -= Arg.Any<AsyncEventHandler<ChannelPointRedeemEventArgs>>();
        }
    }
}
