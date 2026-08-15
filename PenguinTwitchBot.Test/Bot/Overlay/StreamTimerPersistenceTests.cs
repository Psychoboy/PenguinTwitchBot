using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Bot.Overlay;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Test.Bot.Overlay
{
    public class StreamTimerPersistenceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public StreamTimerPersistenceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection), ServiceLifetime.Scoped);
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Close();
            _connection.Dispose();
            GC.SuppressFinalize(this);
        }

        private StreamTimerService CreateService()
        {
            return new StreamTimerService(
                Substitute.For<IHubContext<MainHub>>(),
                _provider.GetRequiredService<IServiceScopeFactory>(),
                Substitute.For<ILogger<StreamTimerService>>());
        }

        [Fact]
        public async Task SetTime_PersistsAcrossRestart()
        {
            var original = CreateService();
            await original.StartAsync(CancellationToken.None);
            await original.ConfigureAsync("down", 420);
            await original.StopAsync(CancellationToken.None);

            var restarted = CreateService();
            await restarted.StartAsync(CancellationToken.None);

            var state = restarted.GetState();
            Assert.Equal(420, state.Seconds, 1);
            Assert.Equal("down", state.Direction);

            await restarted.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunningTimer_IsRestoredStopped()
        {
            var original = CreateService();
            await original.StartAsync(CancellationToken.None);
            await original.StartAsync("up", 60);
            await original.StopAsync(CancellationToken.None);

            var restarted = CreateService();
            await restarted.StartAsync(CancellationToken.None);

            // The timer must never resume on its own after a restart.
            Assert.False(restarted.GetState().IsRunning);

            await restarted.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task AddTime_PersistsAcrossRestart()
        {
            var original = CreateService();
            await original.StartAsync(CancellationToken.None);
            await original.SetTimeAsync(100);
            await original.AddTimeAsync(50);
            await original.StopAsync(CancellationToken.None);

            var restarted = CreateService();
            await restarted.StartAsync(CancellationToken.None);

            Assert.Equal(150, restarted.GetState().Seconds, 1);

            await restarted.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task Restore_WithNoSavedState_LeavesTimerAtZeroAndStopped()
        {
            var service = CreateService();
            await service.StartAsync(CancellationToken.None);

            var state = service.GetState();
            Assert.False(state.IsRunning);
            Assert.Equal(0, state.Seconds);

            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task Persist_WritesASingleSettingRow()
        {
            var service = CreateService();
            await service.StartAsync(CancellationToken.None);

            await service.SetTimeAsync(30);
            await service.AddTimeAsync(15);
            await service.ConfigureAsync("down", 90);
            await service.StopAsync(CancellationToken.None);

            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = await context.Settings.Where(s => s.Name == "StreamTimerState").ToListAsync();

            Assert.Single(rows);
        }
    }
}
