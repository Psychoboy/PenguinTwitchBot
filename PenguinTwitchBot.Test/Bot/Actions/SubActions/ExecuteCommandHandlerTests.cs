using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models;
using System.Collections.Concurrent;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class ExecuteCommandHandlerTests
    {
        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var backbone = Substitute.For<IServiceBackbone>();
            var handler = new ExecuteCommandHandler(backbone);
            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task MissingCommandName_ThrowsException()
        {
            var backbone = Substitute.For<IServiceBackbone>();
            var handler = new ExecuteCommandHandler(backbone);
            var type = new ExecuteCommandType { CommandName = "" };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }

        [Fact]
        public async Task ElevatedCommand_InvalidRank_ThrowsException()
        {
            var backbone = Substitute.For<IServiceBackbone>();
            var handler = new ExecuteCommandHandler(backbone);
            var type = new ExecuteCommandType
            {
                CommandName = "test",
                ElevatedCommand = true,
                RankToExecuteAs = "InvalidRankName"
            };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }

        [Fact]
        public async Task ValidCommand_RunsCommandOnServiceBackbone()
        {
            var backbone = Substitute.For<IServiceBackbone>();
            backbone.BroadcasterName.Returns("broadcaster");
            var handler = new ExecuteCommandHandler(backbone);

            var type = new ExecuteCommandType
            {
                CommandName = "!shoutout",
                Text = "@targetUser hello",
                ElevatedCommand = false
            };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await backbone.Received(1).RunCommand(Arg.Is<CommandEventArgs>(e =>
                e.Command == "shoutout" &&
                e.TargetUser == "targetUser" &&
                e.Args.Count == 2 &&
                e.Args[0] == "@targetUser" &&
                e.Args[1] == "hello" &&
                e.Name == "broadcaster"
            ));
        }

        [Fact]
        public async Task ElevatedCommand_AppliesElevatedPermissions()
        {
            var backbone = Substitute.For<IServiceBackbone>();
            backbone.BroadcasterName.Returns("broadcaster");
            var handler = new ExecuteCommandHandler(backbone);

            var type = new ExecuteCommandType
            {
                CommandName = "admincmd",
                Text = "",
                ElevatedCommand = true,
                RankToExecuteAs = nameof(Rank.Moderator)
            };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await backbone.Received(1).RunCommand(Arg.Is<CommandEventArgs>(e =>
                e.Command == "admincmd" &&
                e.IsMod == true &&
                e.IsBroadcaster == false
            ));
        }
    }
}
