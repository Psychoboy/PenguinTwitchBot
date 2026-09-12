using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Music;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Test.Bot.Commands.Music
{
    public class YtPlayerSongCooldownTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public YtPlayerSongCooldownTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;
            using var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();

            var defaultPlaylist = new MusicPlaylist
            {
                Name = "Default",
                Songs = [new Song { SongId = "song123", Title = "Default Song", Duration = TimeSpan.FromMinutes(3) }]
            };
            context.Playlists.Add(defaultPlaylist);
            context.SaveChanges();

            context.Settings.Add(new Setting { Name = "LastSongList", DataType = Setting.DataTypeEnum.Int, IntSetting = defaultPlaylist.Id!.Value });
            context.SaveChanges();
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        private YtPlayer CreateYtPlayer(
            IBannedSongService bannedSongService,
            ISongCooldownService songCooldownService,
            IServiceBackbone backbone,
            ICommandHandler commandHandler)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["youtubeApi"] = "test-api-key" })
                .Build();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var songRequestsMetric = Substitute.For<PenguinTwitchBot.Bot.Commands.Metrics.SongRequests>(
                scopeFactory,
                backbone,
                commandHandler,
                Substitute.For<IPenguinDispatcher>(),
                Substitute.For<ILogger<PenguinTwitchBot.Bot.Commands.Metrics.SongRequests>>());

            var services = new ServiceCollection();
            services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(new ApplicationDbContext(options)));
            services.AddSingleton(songRequestsMetric);
            var serviceProvider = services.BuildServiceProvider();

            scopeFactory.CreateScope().Returns(_ => serviceProvider.CreateScope());

            return new YtPlayer(
                configuration,
                Substitute.For<ILogger<YtPlayer>>(),
                Substitute.For<IHubContext<YtHub>>(),
                scopeFactory,
                backbone,
                Substitute.For<IPenguinDispatcher>(),
                commandHandler,
                bannedSongService,
                songCooldownService);
        }

        [Fact]
        public async Task AddSongToRequests_WhenCooldownEnabled_AddsSongCooldown()
        {
            var bannedSongService = Substitute.For<IBannedSongService>();
            var cooldownService = Substitute.For<ISongCooldownService>();
            var backbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();

            cooldownService.GetSettingsAsync().Returns(new SongCooldownSettings
            {
                Enabled = true,
                CooldownMinutes = 45
            });

            var ytPlayer = CreateYtPlayer(bannedSongService, cooldownService, backbone, commandHandler);
            var song = new Song { SongId = "dQw4w9WgXcQ", Title = "Never Gonna Give You Up", RequestedBy = "viewer", Duration = TimeSpan.FromMinutes(3) };

            var method = typeof(YtPlayer).GetMethod("AddSongToRequests",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                binder: null,
                types: [typeof(Song)],
                modifiers: null);

            await (Task<int?>)method!.Invoke(ytPlayer, [song])!;

            await cooldownService.Received(1).AddCooldownAsync(
                "dQw4w9WgXcQ",
                "Never Gonna Give You Up",
                Arg.Is<TimeSpan>(t => t.TotalMinutes == 45),
                "viewer");
        }

        [Fact]
        public async Task Veto_WhenExemptSkippedVetoed_ClearsSongCooldown()
        {
            var bannedSongService = Substitute.For<IBannedSongService>();
            var cooldownService = Substitute.For<ISongCooldownService>();
            var backbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();

            cooldownService.GetSettingsAsync().Returns(new SongCooldownSettings
            {
                Enabled = true,
                ExemptSkippedVetoed = true
            });

            var ytPlayer = CreateYtPlayer(bannedSongService, cooldownService, backbone, commandHandler);
            commandHandler.GetCommand("veto").Returns(new Command(new BaseCommandProperties { CommandName = "veto" }, ytPlayer));

            var currentSong = new Song { SongId = "dQw4w9WgXcQ", Title = "Skipped Song", Duration = TimeSpan.FromMinutes(3) };
            var currentSongField = typeof(YtPlayer).GetField("CurrentSong", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            currentSongField!.SetValue(ytPlayer, currentSong);

            var eventArgs = new CommandEventArgs { Command = "veto", Name = "modUser", DisplayName = "modUser" };

            await ytPlayer.OnCommand(null, eventArgs);

            await cooldownService.Received(1).ClearCooldownAsync("dQw4w9WgXcQ");
        }

        [Fact]
        public async Task WrongSong_ClearsSongCooldown()
        {
            var bannedSongService = Substitute.For<IBannedSongService>();
            var cooldownService = Substitute.For<ISongCooldownService>();
            var backbone = Substitute.For<IServiceBackbone>();
            var commandHandler = Substitute.For<ICommandHandler>();

            var ytPlayer = CreateYtPlayer(bannedSongService, cooldownService, backbone, commandHandler);
            commandHandler.GetCommand("wrongsong").Returns(new Command(new BaseCommandProperties { CommandName = "wrongsong" }, ytPlayer));

            var song = new Song { SongId = "dQw4w9WgXcQ", Title = "Test Song", RequestedBy = "viewerUser", Duration = TimeSpan.FromMinutes(3) };
            var requestsField = typeof(YtPlayer).GetField("Requests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var requests = (List<Song>)requestsField!.GetValue(ytPlayer)!;
            requests.Add(song);

            var eventArgs = new CommandEventArgs { Command = "wrongsong", Name = "viewerUser", DisplayName = "viewerUser" };

            await ytPlayer.OnCommand(null, eventArgs);

            await cooldownService.Received().ClearCooldownAsync("dQw4w9WgXcQ");
        }
    }
}
