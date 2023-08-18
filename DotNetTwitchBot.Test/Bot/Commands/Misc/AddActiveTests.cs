using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Misc;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Commands.Misc
{
    public class AddActiveTests
    {
        private readonly ILogger<AddActive> logger;
        private readonly IServiceBackbone serviceBackbone;
        private readonly ICommandHandler commandHandler;
        private readonly ITicketsFeature ticketsFeature;

        public AddActiveTests()
        {
            logger = Substitute.For<ILogger<AddActive>>();
            serviceBackbone = Substitute.For<IServiceBackbone>();
            commandHandler = Substitute.For<ICommandHandler>();
            ticketsFeature = Substitute.For<ITicketsFeature>();
        }

        [Fact]
        public async Task AddActiveTicket_ShouldAddTickets()
        {
            //Arrange
            var dateTime = DateTime.Now.AddSeconds(-10);
            var addActive = new AddActiveStub(dateTime, logger, serviceBackbone, ticketsFeature, commandHandler);
            addActive.ExecuteTimesUntilNow = 2;

            //Act
            addActive.AddActiveTickets(10);
            await addActive.SendTickets();
            //Assert

            await ticketsFeature.Received(1).GiveTicketsToActiveUsers(10);
        }

        [Fact]
        public async Task AddActiveTicket_ShouldAddTicketsMultiple()
        {
            //Arrange
            var dateTime = DateTime.Now.AddSeconds(-10);
            var addActive = new AddActiveStub(dateTime, logger, serviceBackbone, ticketsFeature, commandHandler);
            addActive.ExecuteTimesUntilNow = 3;

            //Act
            addActive.AddActiveTickets(10);
            addActive.AddActiveTickets(10);
            await addActive.SendTickets();
            //Assert

            await ticketsFeature.Received(1).GiveTicketsToActiveUsers(20);
        }

        [Fact]
        public async Task OnCommand_ShouldAddTickets()
        {
            //Arrange
            var dateTime = DateTime.Now.AddSeconds(-10);
            var addActive = new AddActiveStub(dateTime, logger, serviceBackbone, ticketsFeature, commandHandler);
            addActive.ExecuteTimesUntilNow = 2;

            var commandProperties = new BaseCommandProperties { CommandName = "addactive" };
            var commandService = Substitute.For<IBaseCommandService>();
            var command = new Command(commandProperties, commandService);

            commandHandler.GetCommand("addactive").Returns(command);

            //Act
            await addActive.OnCommand(new(), new DotNetTwitchBot.Bot.Events.Chat.CommandEventArgs { Command = "addactive", Args = new List<string> { "10" } });
            await addActive.SendTickets();
            //Assert

            await ticketsFeature.Received(1).GiveTicketsToActiveUsers(10);
        }
    }

    public class AddActiveStub : AddActive
    {
        private readonly DateTime _dateTime;
        private int _executedTimes = 0;

        public AddActiveStub(DateTime dateTime, ILogger<AddActive> logger, IServiceBackbone eventService, ITicketsFeature ticketsFeature, ICommandHandler commandHandler) : base(logger, eventService, ticketsFeature, commandHandler)
        {
            _dateTime = dateTime;
        }

        protected override DateTime GetDateTime()
        {
            _executedTimes++;
            if (_executedTimes >= ExecuteTimesUntilNow)
            {
                return DateTime.Now;
            }
            return _dateTime;

        }

        public int ExecuteTimesUntilNow { get; set; } = 0;
    }
}
