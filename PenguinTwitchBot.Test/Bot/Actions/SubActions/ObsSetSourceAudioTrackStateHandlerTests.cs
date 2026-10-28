using Microsoft.Extensions.Logging;
using NSubstitute;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Obs;
using System.Collections.Concurrent;
using System.Reflection;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class ObsSetSourceAudioTrackStateHandlerTests
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
        public async Task ValidType_SetsAudioTrack()
        {
            var connectionManager = Substitute.For<IOBSConnectionManager>();
            var logger = Substitute.For<ILogger<ObsSetSourceAudioTrackStateHandler>>();
            var handler = new ObsSetSourceAudioTrackStateHandler(connectionManager, logger);

            var connectionId = Guid.CreateVersion7();
            var (connection, mockObs) = CreateConnectedConnection(connectionId, "Main");
            connectionManager.GetManagedConnection(connectionId).Returns(connection);

            var type = new ObsSetSourceAudioTrackStateType { OBSConnectionId = connectionId, InputName = "Mic", TrackNumber = 1, TrackEnabled = true };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            connectionManager.Received(1).GetManagedConnection(connectionId);
            mockObs.Received(1).SetInputAudioTracks("Mic", Arg.Is<JObject>(o => o["1"]!.ToString() == "True"));
        }

        [Fact]
        public async Task InvalidTrackNumber_ThrowsException()
        {
            var connectionManager = Substitute.For<IOBSConnectionManager>();
            var logger = Substitute.For<ILogger<ObsSetSourceAudioTrackStateHandler>>();
            var handler = new ObsSetSourceAudioTrackStateHandler(connectionManager, logger);

            var connectionId = Guid.CreateVersion7();
            var (connection, _) = CreateConnectedConnection(connectionId, "Main");
            connectionManager.GetManagedConnection(connectionId).Returns(connection);

            var type = new ObsSetSourceAudioTrackStateType { OBSConnectionId = connectionId, InputName = "Mic", TrackNumber = 0, TrackEnabled = true };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
