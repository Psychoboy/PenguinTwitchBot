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
    public class ObsSetSourceFilterStateHandlerTests
    {
        private static (ManagedOBSConnection Connection, IOBSWebsocket MockObs) CreateConnectedConnection(int id, string name)
        {
            var config = new OBSConnection { Id = id, Name = name, Url = "ws://localhost:4455", Password = "test", Enabled = true };
            var mockObs = Substitute.For<IOBSWebsocket>();
            var mockLogger = Substitute.For<ILogger<ManagedOBSConnection>>();
            var connection = new ManagedOBSConnection(config, mockObs, mockLogger);
            typeof(ManagedOBSConnection).GetProperty("IsConnected")?.SetValue(connection, true);
            return (connection, mockObs);
        }

        [Fact]
        public async Task ValidType_SetsFilterState()
        {
            var connectionManager = Substitute.For<IOBSConnectionManager>();
            var logger = Substitute.For<ILogger<ObsSetSourceFilterStateHandler>>();
            var handler = new ObsSetSourceFilterStateHandler(connectionManager, logger);

            var (connection, mockObs) = CreateConnectedConnection(1, "Main");
            connectionManager.GetManagedConnection(1).Returns(connection);

            var type = new ObsSetSourceFilterStateType { OBSConnectionId = 1, SourceName = "Mic", FilterName = "NoiseSuppression", FilterEnabled = true };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            connectionManager.Received(1).GetManagedConnection(1);
            mockObs.Received(1).SetSourceFilterEnabled("Mic", "NoiseSuppression", true);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var connectionManager = Substitute.For<IOBSConnectionManager>();
            var logger = Substitute.For<ILogger<ObsSetSourceFilterStateHandler>>();
            var handler = new ObsSetSourceFilterStateHandler(connectionManager, logger);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task MissingSourceName_ThrowsException()
        {
            var connectionManager = Substitute.For<IOBSConnectionManager>();
            var logger = Substitute.For<ILogger<ObsSetSourceFilterStateHandler>>();
            var handler = new ObsSetSourceFilterStateHandler(connectionManager, logger);

            var type = new ObsSetSourceFilterStateType { OBSConnectionId = 1, SourceName = "", FilterName = "NoiseSuppression", FilterEnabled = true };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
