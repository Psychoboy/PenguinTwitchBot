using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Bot.Overlay;

namespace PenguinTwitchBot.Test.Bot.Overlay
{
    public class StreamTimerServiceTests
    {
        private static StreamTimerService CreateService()
        {
            return new StreamTimerService(
                Substitute.For<IHubContext<MainHub>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<ILogger<StreamTimerService>>());
        }

        [Fact]
        public void GetState_IsStoppedAtZero_ByDefault()
        {
            var service = CreateService();

            var state = service.GetState();

            Assert.False(state.IsRunning);
            Assert.Equal(0, state.Seconds);
            Assert.Equal("up", state.Direction);
        }

        [Fact]
        public async Task StartAsync_SetsDirectionAndStartValue()
        {
            var service = CreateService();

            await service.StartAsync("down", 120);

            var state = service.GetState();
            Assert.True(state.IsRunning);
            Assert.Equal("down", state.Direction);
            Assert.Equal(120, state.Seconds);
        }

        [Fact]
        public async Task StopAsync_WithReset_ClearsTheValue()
        {
            var service = CreateService();
            await service.StartAsync("up", 30);

            await service.StopAsync(reset: true);

            var state = service.GetState();
            Assert.False(state.IsRunning);
            Assert.Equal(0, state.Seconds);
        }

        [Fact]
        public async Task AddTimeAsync_IncreasesValue()
        {
            var service = CreateService();
            await service.SetTimeAsync(10);

            await service.AddTimeAsync(45);

            Assert.Equal(55, service.GetState().Seconds, 3);
        }

        [Fact]
        public async Task RemoveTimeAsync_ClampsAtZero()
        {
            var service = CreateService();
            await service.SetTimeAsync(10);

            await service.RemoveTimeAsync(45);

            Assert.Equal(0, service.GetState().Seconds);
        }

        [Fact]
        public async Task CountdownDoesNotGoBelowZero()
        {
            var service = CreateService();
            await service.StartAsync("down", 0.05);

            await Task.Delay(150);
            await service.StopAsync();

            Assert.Equal(0, service.GetState().Seconds);
        }

        [Fact]
        public async Task ConfigureAsync_SetsDirectionAndValueWithoutStarting()
        {
            var service = CreateService();

            await service.ConfigureAsync("down", 300);

            var state = service.GetState();
            Assert.False(state.IsRunning);
            Assert.Equal("down", state.Direction);
            Assert.Equal(300, state.Seconds, 3);
        }

        [Fact]
        public async Task ConfigureAsync_LeavesARunningTimerRunning()
        {
            var service = CreateService();
            await service.StartAsync("up", 10);

            await service.ConfigureAsync("down", 60);

            var state = service.GetState();
            Assert.True(state.IsRunning);
            Assert.Equal("down", state.Direction);
        }
    }
}
