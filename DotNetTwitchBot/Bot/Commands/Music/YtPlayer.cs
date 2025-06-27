using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Repository;
using Google.Apis.YouTube.v3;
using Microsoft.AspNetCore.SignalR;
using Prometheus;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Commands.Music
{
    public class YtPlayer : BaseCommandService, IHostedService
    {
        private readonly IHubContext<YtHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<YtPlayer> _logger;
        private readonly YouTubeService _youtubeService;
        private readonly ICollector<IGauge> SongRequestsInQueue;

        public IGauge SongsInBackupQueueMetric { get; }

        private readonly List<Song> Requests = [];
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        private MusicPlaylist BackupPlaylist = new();
        private ConcurrentQueue<Song> UnplayedSongs = new();
        private PlayerState State = PlayerState.UnStarted;
        private Song? LastSong = null;
        private Song? CurrentSong = null;
        private Song? NextSong = null;
        private readonly List<Song> RecentlyPlayedSongs = [];
        private readonly List<string> SkipVotes = [];
        private readonly TimeLeft timeLeft = new();

        private static readonly string LastSongList = "LastSongList";
        enum PlayerState
        {
            UnStarted = -1,
            Ended = 0,
            Playing = 1,
            Paused = 2,
            Buffer = 3,
            VideoCued = 5
        }

        private class TimeLeft
        {
            public TimeSpan SongTime { get; set; } = new();
            public DateTime StartTime { get; set; } = DateTime.Now;
        }

        public YtPlayer(
            IConfiguration configuration,
            ILogger<YtPlayer> logger,
            IHubContext<YtHub> hubContext,
            IServiceScopeFactory scopeFactory,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler
        ) : base(serviceBackbone, commandHandler, "YtPlayer")
        {
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _youtubeService = new YouTubeService(new Google.Apis.Services.BaseClientService.Initializer
            {
                ApiKey = configuration["youtubeApi"],
                ApplicationName = "DotNetBot"
            });
            SongRequestsInQueue = Prometheus.Metrics.WithManagedLifetime(TimeSpan.FromHours(2)).CreateGauge("song_requests_in_queue", "Song Requests in Queue", labelNames: ["viewer"]).WithExtendLifetimeOnUse();
            SongsInBackupQueueMetric = Prometheus.Metrics.CreateGauge("songs_in_backup_queue", "Songs in Backup Queue");
        }

        private void IncrementSong(Song? song)
        {
            if (song == null) return;
            SongRequestsInQueue.WithLabels(song.RequestedBy).Inc();
        }

        private void DecrementSong(Song? song)
        {
            if (song == null) return;
            SongRequestsInQueue.WithLabels(song.RequestedBy).Dec();
        }

        public async Task<string> GetNextSong()
        {
            Song? song = null;
            List<Song> recentlyPlayedSongs = [];
            try
            {
                await _semaphoreSlim.WaitAsync();
                if (Requests.Count > 0)
                {
                    song = Requests.First();
                    Requests.RemoveAt(0);
                    LastSong = CurrentSong?.CreateDeepCopy();
                    if(LastSong != null)
                    {
                        RecentlyPlayedSongs.Add(LastSong.CreateDeepCopy());
                        if (RecentlyPlayedSongs.Count > 10)
                        {
                            RecentlyPlayedSongs.RemoveAt(0);
                        }
                        recentlyPlayedSongs = [.. RecentlyPlayedSongs];
                    }
                    CurrentSong = song?.CreateDeepCopy();
                    NextSong = Requests.FirstOrDefault()?.CreateDeepCopy();
                    SkipVotes.Clear();
                    DecrementSong(song);
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
            if (song != null)
            {
                await UpdateRequestedSongsState();
                await _hubContext.Clients.All.SendAsync("CurrentSongUpdate", CurrentSong);
                await SendLastPlayedSongs(recentlyPlayedSongs);
                return song.SongId;
            }

            if (UnplayedSongs.IsEmpty)
            {
                await LoadBackupList();
            }
            if (UnplayedSongs.TryDequeue(out var randomSong) == false) return "";
            SongsInBackupQueueMetric.DecTo(UnplayedSongs.Count);
            randomSong.RequestedBy = ServiceBackbone.BotName ?? "TheBot";
            LastSong = CurrentSong?.CreateDeepCopy();
            try
            {
                await _semaphoreSlim.WaitAsync();
                if (LastSong != null)
                {
                    RecentlyPlayedSongs.Add(LastSong.CreateDeepCopy());
                    if (RecentlyPlayedSongs.Count > 10)
                    {
                        RecentlyPlayedSongs.RemoveAt(0);
                    }
                    recentlyPlayedSongs = [.. RecentlyPlayedSongs];
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
            await SendLastPlayedSongs(recentlyPlayedSongs);
            CurrentSong = randomSong.CreateDeepCopy();
            NextSong = null;
            SkipVotes.Clear();
            await _hubContext.Clients.All.SendAsync("CurrentSongUpdate", CurrentSong);
            await UpdateRequestedSongsState();
            return randomSong.SongId;
        }

        public Song? GetCurrentSong()
        {
            return CurrentSong?.CreateDeepCopy();
        }

        private async Task LoadBackupList()
        {

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var lastPlaylist = await db.Settings.Find(x => x.Name.Equals(LastSongList)).FirstOrDefaultAsync();
                if (lastPlaylist != null)
                {
                    var playList = (await db.Playlists.GetAsync(filter: x => x.Id == lastPlaylist.IntSetting, includeProperties: "Songs")).FirstOrDefault();
                    if (playList != null && playList.Songs != null && playList.Songs.Count > 0)
                    {
                        BackupPlaylist = playList;
                        await UpdateRequestedSongsState();
                        await _hubContext.Clients.All.SendAsync("UpdateCurrentPlaylist", BackupPlaylist);
                        UpdateUnplayedSongs();
                        return;
                    }
                }
                {
                    var playList = (await db.Playlists.GetAsync(orderBy: x => x.OrderBy(y => y.Id), includeProperties: "Songs")).FirstOrDefault();
                    if (playList != null && playList.Songs != null && playList.Songs.Count > 0)
                    {
                        BackupPlaylist = playList;
                        await UpdateRequestedSongsState();
                        await _hubContext.Clients.All.SendAsync("UpdateCurrentPlaylist", BackupPlaylist);
                        UpdateUnplayedSongs();
                        return;
                    }
                }
            }

            var song = await GetSong("ZyhrYis509A");
            if (song != null)
                BackupPlaylist.Songs.Add(song);
            song = await GetSong("EAwWPadFsOA");
            if (song != null)
                BackupPlaylist.Songs.Add(song);
            UpdateUnplayedSongs();
        }

        private void UpdateUnplayedSongs()
        {
            var songList = BackupPlaylist.Songs.ToList();
            songList.Shuffle();

            foreach (var nextSong in songList)
            {
                UnplayedSongs.Enqueue(nextSong);
            }
            SongsInBackupQueueMetric.IncTo(UnplayedSongs.Count);
        }

        public async void UpdateState(int state)
        {
            this.State = (PlayerState)state;
            _logger.LogDebug("Player State {this.State}", state);
            if (this.State == PlayerState.Ended)
            {
                await PlayNextSong();
            }

            if (State == PlayerState.Paused)
            {
                if (timeLeft.SongTime.Ticks > 0)
                {
                    timeLeft.SongTime -= DateTime.Now - timeLeft.StartTime;
                    timeLeft.StartTime = DateTime.Now;
                    timeLeft.SongTime = timeLeft.SongTime.Ticks < 0 ? new() : timeLeft.SongTime;
                }
            }
            else if (State == PlayerState.Playing)
            {
                if (timeLeft.SongTime.Ticks > 0)
                {
                    timeLeft.StartTime = DateTime.Now;
                }
            }

        }

        public async Task PlayNextSong()
        {
            await _hubContext.Clients.All.SendAsync("PlayVideo", await GetNextSong());
            if (CurrentSong != null)
            {
                timeLeft.SongTime = CurrentSong.Duration;
                timeLeft.StartTime = DateTime.Now;
            }
            else
            {
                timeLeft.SongTime = new();
                timeLeft.StartTime = DateTime.Now;
            }
        }

        public async Task Pause()
        {
            if (State == PlayerState.Playing)
            {
                await _hubContext.Clients.All.SendAsync("Pause");
            }
            else
            {
                await _hubContext.Clients.All.SendAsync("Play");
            }
        }

        private TimeSpan GetCurrentSongTimeLeft()
        {
            if (timeLeft.SongTime.Ticks <= 0) return new();
            if (State == PlayerState.Paused)
            {
                return timeLeft.SongTime;
            }
            return timeLeft.SongTime - (DateTime.Now - timeLeft.StartTime);
        }

        public async Task SongError(object errorCode)
        {
            _logger.LogWarning("Error with song {errorCode}", errorCode);
            if (CurrentSong != null && !CurrentSong.RequestedBy.Equals(ServiceBackbone.BotName ?? "TheBot"))
            {
                await ServiceBackbone.SendChatMessage(CurrentSong.RequestedBy, $"Could not play your song {CurrentSong.Title} due to an error. Skipping...");
            }
            await PlayNextSong();
        }

        public async Task<MusicPlaylist> CurrentPlaylist()
        {
            if (this.BackupPlaylist.Songs.Count == 0)
            {
                await LoadBackupList();
            }
            return this.BackupPlaylist;
        }
        public async Task<List<MusicPlaylist>> Playlists()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return (await db.Playlists.GetAllAsync()).ToList();
        }

        public async Task<MusicPlaylist> GetPlayList(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return (await db.Playlists.GetAsync(x => x.Id == id, includeProperties: "Songs")).First();
        }

        public async Task AddSongToRequests(string url)
        {
            var song = await GetSongByLinkOrId(url);
            if (song == null)
            {
                _logger.LogWarning("Song was null.");
                return;
            }
            song.RequestedBy = ServiceBackbone.BroadcasterName;
            await AddSongToRequests(song);
        }

        public async Task AddSongToQueue(Song song)
        {
            if (song == null) return;
            await AddSongToRequests(song);
        }

        public async Task MoveSongToNext(string songId)
        {
            Song? song;
            try
            {
                await _semaphoreSlim.WaitAsync();
                song = Requests.Where(x => x.SongId.Equals(songId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (song == null) return;
                Requests.Remove(song);
                Requests.Insert(0, song);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
            NextSong = song?.CreateDeepCopy();
            await UpdateRequestedSongsState();
        }

        public override async Task Register()
        {
            var moduleName = "MusicPlayer";
            await RegisterDefaultCommand("lastsong", this, moduleName);
            await RegisterDefaultCommand("song", this, moduleName);
            await RegisterDefaultCommand("currentsong", this, moduleName);
            await RegisterDefaultCommand("nextsong", this, moduleName);
            await RegisterDefaultCommand("skip", this, moduleName);
            await RegisterDefaultCommand("voteskip", this, moduleName);
            await RegisterDefaultCommand("veto", this, moduleName, Rank.Moderator);
            await RegisterDefaultCommand("pause", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("wrongsong", this, moduleName);
            await RegisterDefaultCommand("wrong", this, moduleName);
            await RegisterDefaultCommand("sr", this, moduleName);
            await RegisterDefaultCommand("priority", this, moduleName, userCooldown: 1800);
            await RegisterDefaultCommand("importpl", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("loadpl", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("stealsong", this, moduleName, Rank.Streamer);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "lastsong":
                    await SayLastSong(e);
                    break;
                case "song":
                case "currentsong":
                    await SaySong(e);
                    break;
                case "nextsong":
                    await SayNextSong(e);
                    break;


                case "skip":
                case "voteskip":
                    await VoteSkipSong(e);
                    break;
                case "veto":
                    await PlayNextSong();
                    break;
                case "pause":
                    await Pause();
                    break;
                case "wrongsong":
                case "wrong":
                    await WrongSong(e);
                    break;
                case "sr":
                    await SongRequest(e);
                    break;
                case "priority":
                    await MovePriority(e);
                    break;
                case "importpl":
                    await ImportPlaylist(e);
                    break;
                case "loadpl":
                    await LoadPlaylist(e);
                    break;

                case "stealsong":
                    await StealCurrentSong();
                    break;
            }
        }

        public async Task RemoveSong(Song requestedSong)
        {
            if (BackupPlaylist.Songs.Count == 0) return;
            var song = BackupPlaylist.Songs.Where(x => x.Id == requestedSong.Id).FirstOrDefault();
            if (song == null)
            {
                await ServiceBackbone.SendChatMessage("Song is not in the list.");
                return;
            }


            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                BackupPlaylist.Songs.Remove(song);
                db.Playlists.Update(BackupPlaylist);
                db.Songs.Remove(song);
                await db.SaveChangesAsync();
            }
            await _hubContext.Clients.All.SendAsync("UpdateCurrentPlaylist", BackupPlaylist);
        }

        public async Task StealCurrentSong()
        {
            if (BackupPlaylist.Songs.Count == 0) return;
            if (CurrentSong == null) return;
            if (BackupPlaylist.Songs.Where(x => x.SongId.Equals(CurrentSong.SongId)).Any())
            {
                await ServiceBackbone.SendChatMessage("Song is already in the list.");
                return;
            }
            var songToSteel = CurrentSong.CreateDeepCopy();
            songToSteel.Id = null;
            songToSteel.RequestedBy = ServiceBackbone.BotName ?? "TheBot";
            BackupPlaylist.Songs.Add(songToSteel);
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.Playlists.Update(BackupPlaylist);
                await db.SaveChangesAsync();
            }
            await _hubContext.Clients.All.SendAsync("UpdateCurrentPlaylist", BackupPlaylist);
        }

        private async Task WrongSong(CommandEventArgs e)
        {
            if (e.Command.Equals("wrong") && e.Arg.StartsWith("song") == false) return;
            Song? song;
            try
            {
                await _semaphoreSlim.WaitAsync();
                song = Requests.Where(x => x.RequestedBy.Equals(e.DisplayName)).LastOrDefault();
            }
            finally { _semaphoreSlim.Release(); }
            if (song != null)
            {
                await RemoveSongRequest(song);
                await UpdateRequestedSongsState();
                await ServiceBackbone.SendChatMessage(e.DisplayName, $"Song {song.Title} was removed");
                return;
            }
            await ServiceBackbone.SendChatMessage(e.DisplayName, "No songs founds");
            throw new SkipCooldownException();
        }

        private async Task<int> GetSongRequestedCount(Song song)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var SongRequestMetrics = scope.ServiceProvider.GetRequiredService<Metrics.SongRequests>();
            return await SongRequestMetrics.GetRequestedCount(song);
        }

        public async Task RemoveSongRequest(Song requestedSong)
        {
            Song? song;
            try
            {
                await _semaphoreSlim.WaitAsync();
                song = Requests.Where(x => x.SongId.Equals(requestedSong.SongId)).FirstOrDefault();
            }
            finally { _semaphoreSlim.Release(); }
            if (song != null)
            {
                try
                {
                    await _semaphoreSlim.WaitAsync();
                    Requests.Remove(song);
                }
                finally { _semaphoreSlim.Release(); }
                DecrementSong(song);
                await UpdateRequestedSongsState();
            }
        }

        private async Task VoteSkipSong(CommandEventArgs e)
        {
            if (SkipVotes.Contains(e.Name))
            {
                throw new SkipCooldownException();
            }
            SkipVotes.Add(e.Name);
            if (SkipVotes.Count >= 3)
            {
                await ServiceBackbone.SendChatMessage($"{e.DisplayName} voted to skip the song and was the 3rd vote. Skipping song.");
                await PlayNextSong();
                return;
            }

            await ServiceBackbone.SendChatMessage($"{e.DisplayName} is trying to skip the current song. {3 - SkipVotes.Count} more votes needed.");
        }

        private async Task SayNextSong(CommandEventArgs e)
        {
            var nextSong = NextSong?.CreateDeepCopy();
            if (nextSong == null)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "There currently is no next song requested.");
                return;
            }
            await ServiceBackbone.SendChatMessage(e.DisplayName, $"The next song is [{nextSong.Title}] requested by {nextSong.RequestedBy} from https://youtu.be/{nextSong.SongId} it has been requested {await GetSongRequestedCount(nextSong)} times");
        }

        private async Task SaySong(CommandEventArgs e)
        {
            var currentSong = CurrentSong?.CreateDeepCopy();
            if (currentSong == null)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "There currently is no current song.");
                return;
            }
            await ServiceBackbone.SendChatMessage(e.DisplayName, $"The current song is [{currentSong.Title}] requested by {currentSong.RequestedBy} from https://youtu.be/{currentSong.SongId} it has been requested {await GetSongRequestedCount(currentSong)} times");
        }

        private async Task SayLastSong(CommandEventArgs e)
        {
            var lastSong = LastSong?.CreateDeepCopy();
            if (lastSong == null)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "There currently is no known last song.");
                return;
            }
            await ServiceBackbone.SendChatMessage(e.DisplayName, $"The last song was [{lastSong.Title}] requested by {lastSong.RequestedBy} from https://youtu.be/{lastSong.SongId} it has been requested {await GetSongRequestedCount(lastSong)} times");
        }

        private async Task LoadPlaylist(CommandEventArgs e)
        {
            await LoadPlayList(e.Arg);
        }

        public async Task LoadPlayList(string name)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var playListId = await db.Playlists.Find(y => y.Name.Equals(name)).Select(x => x.Id).FirstOrDefaultAsync();
            if (playListId != null)
            {
                await LoadPlayList((int)playListId);
            }

        }

        public async Task LoadPlayList(int id)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var playList = (await db.Playlists.GetAsync(filter: x => x.Id == id, includeProperties: "Songs")).FirstOrDefault();
                if (playList == null)
                {
                    await ServiceBackbone.SendChatMessage("No playlist found");
                    return;
                }
                BackupPlaylist = playList;
                var lastPlaylist = await db.Settings.Find(x => x.Name.Equals(LastSongList)).FirstOrDefaultAsync();
                lastPlaylist ??= new Setting
                {
                    Name = LastSongList
                };
                lastPlaylist.IntSetting = playList.Id ?? default;

                db.Settings.Update(lastPlaylist);
                await db.SaveChangesAsync();
            }
            await _hubContext.Clients.All.SendAsync("UpdateCurrentPlaylist", BackupPlaylist);
        }

        private async Task ImportPlaylist(CommandEventArgs e)
        {
            if (e.Args.Count < 2) throw new SkipCooldownException();
            try
            {
                await ServiceBackbone.SendChatMessage("Importing Playlist");
                var playListName = e.Args[0];
                var playListFile = e.Args[1];
                if (string.IsNullOrEmpty(playListFile) || string.IsNullOrWhiteSpace(playListName))
                {
                    await ServiceBackbone.SendChatMessage("Invalid playlist name or file name");
                    return;
                }

                if (File.Exists($"Playlists/{playListFile}") == false)
                {
                    await ServiceBackbone.SendChatMessage("File doesn't exists");
                    return;
                }

                var songLinks = await File.ReadAllLinesAsync($"Playlists/{playListFile}");
                if (songLinks.Length <= 1)
                {
                    await ServiceBackbone.SendChatMessage("No songs in playlist");
                    return;
                }

                MusicPlaylist? playList;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    playList = await db.Playlists.Find(x => x.Name.Equals(playListName)).FirstOrDefaultAsync();
                    playList ??= new MusicPlaylist()
                    {
                        Name = playListName
                    };
                }

                foreach (var songLink in songLinks)
                {
                    var song = await GetSongByLinkOrId(songLink);
                    if (song == null)
                    {
                        continue;
                    }
                    if (playList.Songs.Where(x => x.SongId.Equals(song.SongId)).FirstOrDefault() != null)
                    {
                        continue;
                    }
                    song.RequestedBy = ServiceBackbone.BotName ?? "TheBot";
                    playList.Songs.Add(song);
                }

                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    db.Playlists.Update(playList);
                    await db.SaveChangesAsync();
                }
                await ServiceBackbone.SendChatMessage($"Imported Playlist {playList.Name} with {playList.Songs.Count} songs");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import");
                await ServiceBackbone.SendChatMessage("Failed to import playlist");
            }

        }

        private async Task<Song?> GetSongByLinkOrId(string songLink)
        {
            songLink = songLink.Trim();
            string songId;
            if (songLink.Contains("https://"))
            {
                songId = await GetSongId(songLink);
            }
            else
            {
                songId = songLink.Trim();
            }
            if (string.IsNullOrWhiteSpace(songId))
            {
                return null;
            }
            var song = await GetSong(songId);
            return song;
        }

        private async Task MovePriority(CommandEventArgs e)
        {
            List<Song> backwardsRequest;
            try
            {
                await _semaphoreSlim.WaitAsync();
                backwardsRequest = [.. Requests];
            }
            finally { _semaphoreSlim.Release(); }
            backwardsRequest.Reverse();
            Song? foundSong = null;
            foreach (var song in backwardsRequest)
            {
                if (song.RequestedBy.Equals(e.DisplayName))
                {
                    await MoveSongToNext(song.SongId);
                    foundSong = song;
                    break;
                }
            }
            if (foundSong != null)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, string.Format("{0} was moved to next song.", foundSong.Title));
            }
            else
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "couldn't find a song to prioritize for ya.");
                throw new SkipCooldownException();
            }
        }

        private async Task SongRequest(CommandEventArgs e)
        {
            var songsInQueue = 0;
            try
            {
                await _semaphoreSlim.WaitAsync();
                songsInQueue = Requests.Where(x => x.RequestedBy.Equals(e.DisplayName)).Count();
            }
            finally { _semaphoreSlim.Release(); }
            if (songsInQueue >= 30)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "You already have your quota(30) of songs in the queue.");
                throw new SkipCooldownException();
            }
            var searchResult = await GetSongId(e.Arg);
            if (string.IsNullOrWhiteSpace(searchResult))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "Could not get or had an issue finding your song request");
                throw new SkipCooldownException();
            }
            Song? songInQueue = null;
            try
            {
                await _semaphoreSlim.WaitAsync();
                songInQueue = Requests.Where(x => x.SongId.Equals(searchResult)).FirstOrDefault();
            }
            finally { _semaphoreSlim.Release(); }

            if (songInQueue == null && CurrentSong != null)
            {
                songInQueue = CurrentSong.SongId.Equals(searchResult) ? CurrentSong : null;
            }

            if (songInQueue != null)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, $"That song is already in the queue.");
                throw new SkipCooldownException();
            }

            var song = await GetSong(searchResult, e.DisplayName);
            if (song == null)
            {
                return;
            }
            if (song.Duration > new TimeSpan(0, 10, 0) || song.Duration == new TimeSpan(0, 0, 0))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, string.Format("Your song is to long or is live. Max is 10 minutes and yours is: {0:c}", song.Duration));
                return;
            }

            List<Song> currentRequestedSongs;
            int requestCount;
            try
            {
                await _semaphoreSlim.WaitAsync();
                currentRequestedSongs = [.. Requests];
                requestCount = Requests.Count;
            }
            finally { _semaphoreSlim.Release(); }

            var timeToWait = new TimeSpan(currentRequestedSongs.Sum(r => r.Duration.Ticks));
            timeToWait += GetCurrentSongTimeLeft();

            await AddSongToRequests(song);
            if (e.IsWhisper) return;

            requestCount++;

            await ServiceBackbone.SendChatMessageWithTitle(e.Name, string.Format("{0} was added in position #{1}, you have a total of {2} requested. Will play in ~{3}. It has been requested {4} times.", song.Title, requestCount, songsInQueue + 1, timeToWait.ToFriendlyString(), await GetSongRequestedCount(song)));
        }

        private async Task AddSongToRequests(Song song)
        {
            try
            {
                await _semaphoreSlim.WaitAsync();
                Requests.Add(song);
            }
            finally { _semaphoreSlim.Release(); }
            IncrementSong(song);
            await UpdateRequestedSongsState();
            NextSong ??= song;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var SongRequestMetrics = scope.ServiceProvider.GetRequiredService<Metrics.SongRequests>();
            await SongRequestMetrics.IncrementSongCount(song);
        }

        private static async Task<string> GetSongId(string searchTerm)
        {
            var client = new HttpClient();
            var httpRequest = new HttpRequestMessage()
            {
                RequestUri = new Uri(string.Format("https://decapi.me/youtube/videoid?search={0}", Uri.EscapeDataString(searchTerm))),
                Method = HttpMethod.Get
            };
            var searchResponse = await client.SendAsync(httpRequest);
            var searchResult = await searchResponse.Content.ReadAsStringAsync();
            return searchResult;
        }

        private async Task<Song?> GetSong(string youtubeId, string? displayName = null, bool sendChatResponse = true)
        {
            var ytRequest = _youtubeService.Videos.List("snippet,contentDetails");
            ytRequest.Id = youtubeId;

            var ytResponse = await ytRequest.ExecuteAsync();
            if (ytResponse != null && ytResponse.Items.Count > 0)
            {
                var item = ytResponse.Items.First();
                TimeSpan length = new();
                if (item.ContentDetails.ContentRating.YtRating?.Equals("ytAgeRestricted") == true)
                {
                    if (sendChatResponse) await ServiceBackbone.SendChatMessage("That song can not be played due to restrictions.");
                    return null;
                }
                if (item.AgeGating != null && item.AgeGating.Restricted == true)
                {
                    if (sendChatResponse) await ServiceBackbone.SendChatMessage("That song can not be played due to restrictions.");
                    return null;
                }
                if (Iso8601DurationHelper.Duration.TryParse(item.ContentDetails.Duration, out var duration))
                {
                    length = new TimeSpan((int)duration.Hours, (int)duration.Minutes, (int)duration.Seconds);
                }

                var song = new Song
                {
                    Title = item.Snippet.Title,
                    Duration = length,
                    SongId = youtubeId
                };
                if (displayName != null) song.RequestedBy = displayName;
                return song;
            }
            if (displayName != null)
            {
                if (sendChatResponse) await ServiceBackbone.SendChatMessage(displayName, "Could not get or had an issue finding your song request");
            }
            return null;
        }

        public List<Song> GetRequestedSongs()
        {
            try
            {
                _semaphoreSlim.Wait();
                return [.. Requests];
            }
            finally { _semaphoreSlim.Release(); }
        }

        public List<Song> GetRecentlyPlayedSongs()
        {
            try
            {
                _semaphoreSlim.Wait();
                List<Song> songs = [.. RecentlyPlayedSongs.Select(x => x.CreateDeepCopy())];
                songs.Reverse();
                return songs;
            }
            finally { _semaphoreSlim.Release(); }
        }

        private async Task UpdateRequestedSongsState()
        {
            List<Song> requests;
            try
            {
                await _semaphoreSlim.WaitAsync();
                requests = [.. Requests];
            }
            finally { _semaphoreSlim.Release(); }
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var id = 1;
            if (requests.Count == 0)
            {
                db.SongRequestViewItems.RemoveRange(db.SongRequestViewItems.GetAll());
                await SendSongRequests(requests);
                await db.SaveChangesAsync();
                return;
            }
            foreach (var request in requests)
            {
                bool newRecord = false;
                var item = await db.SongRequestViewItems.Find(x => x.Id == id).FirstOrDefaultAsync();
                if (item == null)
                {
                    item = new SongRequestViewItem
                    {
                        Id = id
                    };
                    newRecord = true;
                }
                item.Duration = request.Duration;
                item.RequestedBy = request.RequestedBy;
                item.SongId = request.SongId;
                item.Title = request.Title;
                if (newRecord)
                {
                    db.SongRequestViewItems.Add(item);
                }
                else
                {
                    db.SongRequestViewItems.Update(item);
                }
                id++;
            }
            db.SongRequestViewItems.RemoveRange(db.SongRequestViewItems.Find(x => x.Id >= id));
            await SendSongRequests(requests);
            await db.SaveChangesAsync();
        }

        private async Task SendSongRequests(List<Song> requests)
        {
            await _hubContext.Clients.All.SendAsync("CurrentSongRequests", requests);
        }

        private Task SendLastPlayedSongs(List<Song> songs)
        {
            return _hubContext.Clients.All.SendAsync("LastPlayedSongs", songs);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}