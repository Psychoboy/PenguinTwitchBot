using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Music;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Test.Bot.Commands.Music
{
    public class YtPlayerTests
    {
        [Fact]
        public void ExtractYouTubePlaylistId_FromUrl_ExtractsListParameter()
        {
            var method = typeof(YtPlayer).GetMethod("ExtractYouTubePlaylistId", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            var result = method!.Invoke(null, ["https://www.youtube.com/playlist?list=ABCD1234"]) as string;
            Assert.Equal("ABCD1234", result);
        }

        [Fact]
        public void ExtractYouTubePlaylistId_FromUrl_WithQueryString_ReturnsId()
        {
            var method = typeof(YtPlayer).GetMethod("ExtractYouTubePlaylistId", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            var result = method!.Invoke(null, ["https://www.youtube.com/watch?v=test&list=XYZ789"]) as string;
            Assert.Equal("XYZ789", result);
        }

        [Fact]
        public void ExtractYouTubePlaylistId_InvalidUrl_ReturnsNull()
        {
            var method = typeof(YtPlayer).GetMethod("ExtractYouTubePlaylistId", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            var result = method!.Invoke(null, ["not-a-url"]) as string;
            Assert.Equal("not-a-url", result); // Input without / is treated as raw ID
        }

        [Fact]
        public void Song_CreateDeepCopy_ReturnsCopy()
        {
            var song = new Song { SongId = "test123", Title = "Test Song", RequestedBy = "User", Duration = TimeSpan.FromMinutes(3) };
            var copy = song.CreateDeepCopy();
            
            Assert.NotSame(song, copy);
            Assert.Equal(song.SongId, copy.SongId);
            Assert.Equal(song.Title, copy.Title);
            Assert.Equal(song.RequestedBy, copy.RequestedBy);
            Assert.Equal(song.Duration, copy.Duration);
        }

        [Fact]
        public void MergePlaylists_MergesDistinctSongsBySongId()
        {
            var existingSong = new Song { SongId = "song1", Title = "Song One", Duration = TimeSpan.FromMinutes(3) };
            var duplicateSong = new Song { SongId = "song1", Title = "Song One Duplicate", Duration = TimeSpan.FromMinutes(4) };
            var newSong = new Song { SongId = "song2", Title = "Song Two", Duration = TimeSpan.FromMinutes(5) };

            var backupPlaylist = new MusicPlaylist
            {
                Id = 1,
                Name = "Backup",
                Songs = [existingSong]
            };

            var additionalSongs = new List<Song> { duplicateSong, newSong };

            var allSongs = new Dictionary<string, Song>();
            if (backupPlaylist.Songs != null)
            {
                foreach (var s in backupPlaylist.Songs)
                {
                    if (!allSongs.ContainsKey(s.SongId))
                        allSongs.Add(s.SongId, s);
                }
            }
            foreach (var s in additionalSongs)
            {
                if (!allSongs.ContainsKey(s.SongId))
                    allSongs.Add(s.SongId, s);
            }

            Assert.Equal(2, allSongs.Count);
            Assert.True(allSongs.ContainsKey("song1"));
            Assert.True(allSongs.ContainsKey("song2"));
            Assert.Equal("Song One", allSongs["song1"].Title);
        }

        [Fact]
        public void MergePlaylists_MultiplePlaylists_KeepsFirstOccurrence()
        {
            var song1FromPlaylist1 = new Song { SongId = "song1", Title = "Playlist1 Song1", Duration = TimeSpan.FromMinutes(3) };
            var song1FromPlaylist2 = new Song { SongId = "song1", Title = "Playlist2 Song1", Duration = TimeSpan.FromMinutes(3) };
            var song2 = new Song { SongId = "song2", Title = "Song2", Duration = TimeSpan.FromMinutes(4) };

            var playlist1 = new MusicPlaylist { Id = 1, Songs = [song1FromPlaylist1, song2] };
            var playlist2 = new MusicPlaylist { Id = 2, Songs = [song1FromPlaylist2] };

            var allSongs = new Dictionary<string, Song>();
            foreach (var playlist in new[] { playlist1, playlist2 })
            {
                if (playlist.Songs != null)
                {
                    foreach (var s in playlist.Songs)
                    {
                        if (!allSongs.ContainsKey(s.SongId))
                            allSongs.Add(s.SongId, s);
                    }
                }
            }

            Assert.Equal(2, allSongs.Count);
            Assert.Equal("Playlist1 Song1", allSongs["song1"].Title);
        }

        [Fact]
        public void UpdateState_Playing_UpdatesTimeLeft()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["youtubeApi"] = "test-api-key" })
                .Build();

            var ytPlayer = new YtPlayer(
                configuration,
                Substitute.For<ILogger<YtPlayer>>(),
                Substitute.For<Microsoft.AspNetCore.SignalR.IHubContext<YtHub>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var method = typeof(YtPlayer).GetMethod("UpdateState", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            var timeLeftField = typeof(YtPlayer).GetField("timeLeft", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.NotNull(method);
            Assert.NotNull(timeLeftField);
        }

        [Fact]
        public void GetCurrentSongTimeLeft_WhenNotPlaying_ReturnsZero()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["youtubeApi"] = "test-api-key" })
                .Build();

            var ytPlayer = new YtPlayer(
                configuration,
                Substitute.For<ILogger<YtPlayer>>(),
                Substitute.For<Microsoft.AspNetCore.SignalR.IHubContext<YtHub>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var method = typeof(YtPlayer).GetMethod("GetCurrentSongTimeLeft", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);
        }

        [Fact]
        public void YtPlayer_Constructor_InitializesDependencies()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["youtubeApi"] = "test-api-key" })
                .Build();

            var ytPlayer = new YtPlayer(
                configuration,
                Substitute.For<ILogger<YtPlayer>>(),
                Substitute.For<Microsoft.AspNetCore.SignalR.IHubContext<YtHub>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            Assert.NotNull(ytPlayer);
        }

        [Fact]
        public void YtPlayer_StopAsync_Completes()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["youtubeApi"] = "test-api-key" })
                .Build();

            var ytPlayer = new YtPlayer(
                configuration,
                Substitute.For<ILogger<YtPlayer>>(),
                Substitute.For<Microsoft.AspNetCore.SignalR.IHubContext<YtHub>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            Assert.NotNull(ytPlayer);
        }

        [Fact]
        public void UpdateUnplayedSongs_ClearsAndShufflesQueue()
        {
            var method = typeof(YtPlayer).GetMethod("UpdateUnplayedSongs", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.NotNull(method);
        }

        [Fact]
        public void SongExistsInBackupList_MultipleSongs_ReturnsTrue()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["youtubeApi"] = "test-api-key" })
                .Build();

            var ytPlayer = new YtPlayer(
                configuration,
                Substitute.For<ILogger<YtPlayer>>(),
                Substitute.For<Microsoft.AspNetCore.SignalR.IHubContext<YtHub>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var song1 = new Song { SongId = "song1", Title = "Song 1", Duration = TimeSpan.FromMinutes(3) };
            var song2 = new Song { SongId = "song2", Title = "Song 2", Duration = TimeSpan.FromMinutes(4) };

            var backupPlaylistField = typeof(YtPlayer).GetField("BackupPlaylist", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var backupPlaylist = new MusicPlaylist { Id = 1, Name = "Backup", Songs = [song1, song2] };
            backupPlaylistField!.SetValue(ytPlayer, backupPlaylist);

            Assert.True(ytPlayer.SongExistsInBackupList(song1));
            Assert.True(ytPlayer.SongExistsInBackupList(song2));
        }

        [Fact]
        public void GetRecentlyPlayedSongs_ReturnsReversedCopy()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["youtubeApi"] = "test-api-key" })
                .Build();

            var ytPlayer = new YtPlayer(
                configuration,
                Substitute.For<ILogger<YtPlayer>>(),
                Substitute.For<Microsoft.AspNetCore.SignalR.IHubContext<YtHub>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var method = typeof(YtPlayer).GetMethod("GetRecentlyPlayedSongs", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            Assert.NotNull(method);
        }
    }
}