using Microsoft.Extensions.Logging;
using NSubstitute;
using OBSWebsocketDotNet;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Obs;
using System.Collections.Concurrent;
using System.Reflection;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class ObsSetColorSourceColorHandlerTests
    {
        private static ManagedOBSConnection CreateConnectedConnection(int id, string name)
        {
            var config = new OBSConnection { Id = id, Name = name, Url = "ws://localhost:4455", Password = "test", Enabled = true };
            var mockObs = Substitute.For<IOBSWebsocket>();
            var mockLogger = Substitute.For<ILogger<ManagedOBSConnection>>();
            var connection = new ManagedOBSConnection(config, mockObs, mockLogger);
            typeof(ManagedOBSConnection).GetProperty("IsConnected")?.SetValue(connection, true);
            return connection;
        }

        [Fact]
        public async Task ValidType_SetsColor()
        {
            var connectionManager = Substitute.For<IOBSConnectionManager>();
            var logger = Substitute.For<ILogger<ObsSetColorSourceColorHandler>>();
            var handler = new ObsSetColorSourceColorHandler(connectionManager, logger);

            var connection = CreateConnectedConnection(1, "Main");
            connectionManager.GetManagedConnection(1).Returns(connection);

            var type = new ObsSetColorSourceColorType { OBSConnectionId = 1, InputName = "Color", Color = "#FF0000" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            connectionManager.Received(1).GetManagedConnection(1);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var connectionManager = Substitute.For<IOBSConnectionManager>();
            var logger = Substitute.For<ILogger<ObsSetColorSourceColorHandler>>();
            var handler = new ObsSetColorSourceColorHandler(connectionManager, logger);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task InvalidColorFormat_ThrowsException()
        {
            var connectionManager = Substitute.For<IOBSConnectionManager>();
            var logger = Substitute.For<ILogger<ObsSetColorSourceColorHandler>>();
            var handler = new ObsSetColorSourceColorHandler(connectionManager, logger);

            var connection = CreateConnectedConnection(1, "Main");
            connectionManager.GetManagedConnection(1).Returns(connection);

            var type = new ObsSetColorSourceColorType { OBSConnectionId = 1, InputName = "Color", Color = "invalid" };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
