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
    public class ObsSetSceneFilterStateHandlerTests
    {
        private static (ManagedOBSConnection Connection, IOBSWebsocket MockObs) CreateConnectedConnection(Guid id, string name)
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
            var logger = Substitute.For<ILogger<ObsSetSceneFilterStateHandler>>();
            var handler = new ObsSetSceneFilterStateHandler(connectionManager, logger);

            var connectionId = Guid.CreateVersion7();
            var (connection, mockObs) = CreateConnectedConnection(connectionId, "Main");
            connectionManager.GetManagedConnection(connectionId).Returns(connection);

            var type = new ObsSetSceneFilterStateType { OBSConnectionId = connectionId, SceneName = "Scene", FilterName = "Blur", FilterEnabled = true };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            connectionManager.Received(1).GetManagedConnection(connectionId);
            mockObs.Received(1).SetSourceFilterEnabled("Scene", "Blur", true);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var connectionManager = Substitute.For<IOBSConnectionManager>();
            var logger = Substitute.For<ILogger<ObsSetSceneFilterStateHandler>>();
            var handler = new ObsSetSceneFilterStateHandler(connectionManager, logger);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task MissingFilterName_ThrowsException()
        {
            var connectionManager = Substitute.For<IOBSConnectionManager>();
            var logger = Substitute.For<ILogger<ObsSetSceneFilterStateHandler>>();
            var handler = new ObsSetSceneFilterStateHandler(connectionManager, logger);

            var connectionId = Guid.CreateVersion7();
            var (connection, _) = CreateConnectedConnection(connectionId, "Main");
            connectionManager.GetManagedConnection(connectionId).Returns(connection);

            var type = new ObsSetSceneFilterStateType { OBSConnectionId = connectionId, SceneName = "Scene", FilterName = "", FilterEnabled = true };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
