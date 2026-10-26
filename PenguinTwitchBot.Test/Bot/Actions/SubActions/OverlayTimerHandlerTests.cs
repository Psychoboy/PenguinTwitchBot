using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Bot.Overlay;
using PenguinTwitchBot.Database.Bot.Actions.SubActions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class OverlayTimerHandlerTests
    {
        private static StreamTimerService CreateTimerService()
        {
            return new StreamTimerService(
                Substitute.For<IHubContext<MainHub>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<ILogger<StreamTimerService>>());
        }

        private static ConcurrentDictionary<string, string> NoVariables() => new();

        [Fact]
        public async Task AddTimeHandler_AddsTheConfiguredAmount()
        {
            var timerService = CreateTimerService();
            var handler = new OverlayTimerAddTimeHandler(timerService);

            await handler.ExecuteAsync(new OverlayTimerAddTimeType { Amount = "90" }, NoVariables());

            Assert.Equal(90, timerService.GetState().Seconds, 3);
        }

        [Fact]
        public async Task AddTimeHandler_AcceptsHmsFormatAndVariables()
        {
            var timerService = CreateTimerService();
            var handler = new OverlayTimerAddTimeHandler(timerService);
            var variables = new ConcurrentDictionary<string, string> { ["bonus"] = "00:02:30" };

            await handler.ExecuteAsync(new OverlayTimerAddTimeType { Amount = "%bonus%" }, variables);

            Assert.Equal(150, timerService.GetState().Seconds, 3);
        }

        [Fact]
        public async Task RemoveTimeHandler_SubtractsAndClampsAtZero()
        {
            var timerService = CreateTimerService();
            await timerService.SetTimeAsync(100);

            await new OverlayTimerRemoveTimeHandler(timerService)
                .ExecuteAsync(new OverlayTimerRemoveTimeType { Amount = "40" }, NoVariables());

            Assert.Equal(60, timerService.GetState().Seconds, 3);
        }

        [Fact]
        public async Task StartHandler_StartsWithDirectionAndValue()
        {
            var timerService = CreateTimerService();

            await new OverlayTimerStartHandler(timerService)
                .ExecuteAsync(new OverlayTimerStartType { Direction = "down", StartTime = "00:05:00" }, NoVariables());

            var state = timerService.GetState();
            Assert.True(state.IsRunning);
            Assert.Equal("down", state.Direction);
            Assert.Equal(300, state.Seconds, 3);
        }

        [Fact]
        public async Task StopHandler_StopsTheTimer()
        {
            var timerService = CreateTimerService();
            await timerService.StartAsync("up", 10);

            await new OverlayTimerStopHandler(timerService)
                .ExecuteAsync(new OverlayTimerStopType(), NoVariables());

            Assert.False(timerService.GetState().IsRunning);
        }

        [Theory]
        [InlineData(SubActionTypes.OverlayTimerStart)]
        [InlineData(SubActionTypes.OverlayTimerStop)]
        [InlineData(SubActionTypes.OverlayTimerAddTime)]
        [InlineData(SubActionTypes.OverlayTimerRemoveTime)]
        public void OverlayTimerSubActions_AreDiscoverable(SubActionTypes subActionType)
        {
            Assert.NotNull(SubActionRegistry.GetMetadata(subActionType));
            Assert.NotNull(SubActionRegistry.GetSubActionType(subActionType));
        }

        [Theory]
        [InlineData(typeof(OverlayTimerStartHandler), SubActionTypes.OverlayTimerStart)]
        [InlineData(typeof(OverlayTimerStopHandler), SubActionTypes.OverlayTimerStop)]
        [InlineData(typeof(OverlayTimerAddTimeHandler), SubActionTypes.OverlayTimerAddTime)]
        [InlineData(typeof(OverlayTimerRemoveTimeHandler), SubActionTypes.OverlayTimerRemoveTime)]
        public void OverlayTimerHandlers_AreRegisteredAndResolvableByConcreteType(Type handlerType, SubActionTypes subActionType)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IStreamTimerService>(CreateTimerService());
            services.AddSubActionHandlers();

            // Resolving ISubActionHandler enumerates every handler, so assert on the registration instead.
            Assert.Contains(services, d => d.ServiceType == typeof(ISubActionHandler) && d.ImplementationType == handlerType);

            using var provider = services.BuildServiceProvider();
            var handler = (ISubActionHandler)provider.GetRequiredService(handlerType);

            Assert.Equal(subActionType, handler.SupportedType);
        }

        [Fact]
        public async Task Handlers_TargetSpecifiedTimerName()
        {
            var timerService = CreateTimerService();
            await timerService.CreateTimerAsync("Subathon");

            var addHandler = new OverlayTimerAddTimeHandler(timerService);
            await addHandler.ExecuteAsync(new OverlayTimerAddTimeType { TimerName = "Subathon", Amount = "120" }, NoVariables());

            Assert.Equal(0, timerService.GetState("Default").Seconds);
            Assert.Equal(120, timerService.GetState("Subathon").Seconds);

            var startHandler = new OverlayTimerStartHandler(timerService);
            await startHandler.ExecuteAsync(new OverlayTimerStartType { TimerName = "Subathon", Direction = "down" }, NoVariables());

            Assert.False(timerService.GetState("Default").IsRunning);
            Assert.True(timerService.GetState("Subathon").IsRunning);

            var stopHandler = new OverlayTimerStopHandler(timerService);
            await stopHandler.ExecuteAsync(new OverlayTimerStopType { TimerName = "Subathon" }, NoVariables());

            Assert.False(timerService.GetState("Subathon").IsRunning);
        }

        [Fact]
        public async Task Handlers_ResolveVariablesInTimerName()
        {
            var timerService = CreateTimerService();
            var variables = new ConcurrentDictionary<string, string> { ["TargetTimer"] = "BossFight" };

            var addHandler = new OverlayTimerAddTimeHandler(timerService);
            await addHandler.ExecuteAsync(new OverlayTimerAddTimeType { TimerName = "%TargetTimer%", Amount = "75" }, variables);

            Assert.Equal(75, timerService.GetState("BossFight").Seconds);
        }
    }
}
