using System.Net;
using System.Runtime.InteropServices.ComTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using Microsoft.AspNetCore.SignalR;
using Google.Apis.YouTube.v3;
using DotNetTwitchBot.Bot.Commands.Features;

namespace DotNetTwitchBot.Bot.Commands.Music
{
    public class YtPlayer : BaseCommand
    {
        private IHubContext<YtHub> _hubContext;
        private IServiceScopeFactory _scopeFactory;
        private ILogger<YtPlayer> _logger;
        private YouTubeService _youtubeService;
        private List<Song> Requests = new List<Song>();
        private readonly object RequestsLock = new object();
        private MusicPlaylist BackupPlaylist = new MusicPlaylist();
        private PlayerState State = PlayerState.UnStarted;
        private Song? LastSong = null;
        private Song? CurrentSong = null;
        private Song? NextSong = null;
        private List<string> SkipVotes = new List<string>();
        enum PlayerState
        {
            UnStarted = -1,
            Ended = 0,
            Playing = 1,
            Paused = 2,
            Buffer = 3,
            VideoCued = 5
        }

        public YtPlayer(
            IConfiguration configuration,
            ILogger<YtPlayer> logger,
            IHubContext<YtHub> hubContext,
            IServiceScopeFactory scopeFactory,
            ServiceBackbone serviceBackbone
        ) : base(serviceBackbone)
        {
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _youtubeService = new YouTubeService(new Google.Apis.Services.BaseClientService.Initializer()
            {
                ApiKey = configuration["youtubeApi"],
                ApplicationName = "DotNetBot"
            });
        }

        public async Task<string> GetNextSong()
        {
            Song? song = null;
            lock (RequestsLock)
            {
                if (Requests.Count > 0)
                {
                    song = Requests.First();
                    Requests.RemoveAt(0);
                    LastSong = CurrentSong;
                    CurrentSong = song;
                    NextSong = Requests.FirstOrDefault();
                    SkipVotes.Clear();
                }
            }
            if (song != null)
            {
                await UpdateDbState();
                await _hubContext.Clients.All.SendAsync("CurrentSongUpdate", CurrentSong);
                return song.SongId;
            }


            if (BackupPlaylist.Songs.Count == 0)
            {
                await LoadBackupList();
            }
            var randomSong = BackupPlaylist.Songs.RandomElement();
            LastSong = CurrentSong;
            CurrentSong = randomSong;
            NextSong = null;
            SkipVotes.Clear();
            await _hubContext.Clients.All.SendAsync("CurrentSongUpdate", CurrentSong);
            await UpdateDbState();
            return randomSong.SongId;
        }

        public Song? GetCurrentSong()
        {
            return this.CurrentSong;
        }

        private async Task LoadBackupList()
        {

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var lastPlaylist = await db.Settings.FirstOrDefaultAsync(x => x.Name.Equals("LastSongList"));
                if (lastPlaylist != null)
                {
                    var playList = await db.Playlists.Include(x => x.Songs).FirstOrDefaultAsync(x => x.Id == lastPlaylist.IntSetting);
                    if (playList != null && playList.Songs != null && playList.Songs.Count > 0)
                    {
                        BackupPlaylist = playList;
                        await UpdateDbState();
                        await _hubContext.Clients.All.SendAsync("UpdateCurrentPlaylist", BackupPlaylist);
                        return;
                    }
                }
                {
                    var playList = await db.Playlists.Include(x => x.Songs).OrderBy(x => x.Id).FirstOrDefaultAsync();
                    if (playList != null && playList.Songs != null && playList.Songs.Count > 0)
                    {
                        BackupPlaylist = playList;
                        await UpdateDbState();
                        await _hubContext.Clients.All.SendAsync("UpdateCurrentPlaylist", BackupPlaylist);
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
        }

        public async void UpdateState(int state)
        {
            this.State = (PlayerState)state;
            _logger.LogInformation($"Player State {this.State}");
            if (this.State == PlayerState.Ended)
            {
                await PlayNextSong();
            }
        }

        public async Task PlayNextSong()
        {
            await _hubContext.Clients.All.SendAsync("PlayVideo", await GetNextSong());
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

        public async Task LoadNextSong()
        {
            await _hubContext.Clients.All.SendAsync("PlayVideo", await GetNextSong());
        }

        public async Task SongError(object errorCode)
        {
            _logger.LogWarning("Error with song {0}", errorCode);
            if (CurrentSong != null)
            {
                await _serviceBackbone.SendChatMessage(CurrentSong.RequestedBy, $"Could not play your song {CurrentSong.Title} due to an error. Skipping...");
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
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.Playlists.ToListAsync();
            }
        }

        public async Task<MusicPlaylist> GetPlayList(int id)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.Playlists.Include(x => x.Songs).Where(x => x.Id == id).FirstAsync();
            }
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
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
                    if (e.isMod || e.isBroadcaster)
                    {
                        await PlayNextSong();
                    }
                    break;
                case "pause":
                    if (e.isBroadcaster)
                    {
                        await Pause();
                    }
                    break;
                case "wrongsong":
                    await WrongSong(e);
                    break;
                case "sr":
                    await SongRequest(e);
                    break;
                case "priority":
                    await MovePriority(e);
                    break;
                case "importpl":
                    if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
                    if (e.Args.Count < 2) return;
                    await ImportPlaylist(e);
                    break;
                case "loadpl":
                    if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
                    await LoadPlaylist(e);
                    break;

                case "steal":
                    if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
                    await StealCurrentSong();
                    break;
            }
        }

        public async Task RemoveSong(Song song)
        {
            if (BackupPlaylist.Songs.Count == 0) return;
            if (BackupPlaylist.Songs.Contains(song) == false)
            {
                await _serviceBackbone.SendChatMessage("Song is not in the list.");
                return;
            }

            BackupPlaylist.Songs.Remove(song);
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Playlists.Update(BackupPlaylist);
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
                await _serviceBackbone.SendChatMessage("Song is already in the list.");
                return;
            }
            CurrentSong.Id = null;
            BackupPlaylist.Songs.Add(CurrentSong);
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Playlists.Update(BackupPlaylist);
                await db.SaveChangesAsync();
            }
            await _hubContext.Clients.All.SendAsync("UpdateCurrentPlaylist", BackupPlaylist);
        }

        private async Task WrongSong(CommandEventArgs e)
        {
            Song? song;
            lock (RequestsLock)
            {
                song = Requests.Where(x => x.RequestedBy.Equals(e.DisplayName)).LastOrDefault();
            }
            if (song != null)
            {
                lock (RequestsLock)
                {
                    Requests.Remove(song);
                }
                await UpdateDbState();
                await _serviceBackbone.SendChatMessage(e.DisplayName, $"Song {song.Title} was removed");
                return;
            }
            await _serviceBackbone.SendChatMessage(e.DisplayName, "No songs founds");
        }

        private async Task VoteSkipSong(CommandEventArgs e)
        {
            if (SkipVotes.Contains(e.Name))
            {
                return;
            }
            SkipVotes.Add(e.Name);
            if (SkipVotes.Count >= 3)
            {
                await _serviceBackbone.SendChatMessage($"{e.DisplayName} voted to skip the song and was the 3rd vote. Skipping song.");
                await PlayNextSong();
                return;
            }

            await _serviceBackbone.SendChatMessage($"{e.DisplayName} is trying to skip the current song. {3 - SkipVotes.Count} more votes needed.");
        }

        private async Task SayNextSong(CommandEventArgs e)
        {
            var nextSong = NextSong;
            if (nextSong == null)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "There currently is no next song requested.");
                return;
            }
            await _serviceBackbone.SendChatMessage(e.DisplayName, $"The next song is [{nextSong.Title}] requested by {nextSong.RequestedBy} from https://youtu.be/{nextSong.SongId}");
        }

        private async Task SaySong(CommandEventArgs e)
        {
            var currentSong = CurrentSong;
            if (currentSong == null)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "There currently is no current song.");
                return;
            }
            await _serviceBackbone.SendChatMessage(e.DisplayName, $"The current song was [{currentSong.Title}] requested by {currentSong.RequestedBy} from https://youtu.be/{currentSong.SongId}");
        }

        private async Task SayLastSong(CommandEventArgs e)
        {
            var lastSong = LastSong;
            if (lastSong == null)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "There currently is no known last song.");
                return;
            }
            await _serviceBackbone.SendChatMessage(e.DisplayName, $"The last song was [{lastSong.Title}] requested by {lastSong.RequestedBy} from https://youtu.be/{lastSong.SongId}");
        }

        private async Task LoadPlaylist(CommandEventArgs e)
        {
            await LoadPlayList(e.Arg);
        }

        public async Task LoadPlayList(string name)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var playListId = await db.Playlists.Where(y => y.Name.Equals(name)).Select(x => x.Id).FirstOrDefaultAsync();
                if (playListId != null)
                {
                    await LoadPlayList((int)playListId);
                }
            }

        }

        public async Task LoadPlayList(int id)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var playList = await db.Playlists.Include(x => x.Songs).FirstOrDefaultAsync(x => x.Id == id);
                if (playList == null)
                {
                    await _serviceBackbone.SendChatMessage("No playlist found");
                    return;
                }
                BackupPlaylist = playList;
                var lastPlaylist = await db.Settings.FirstOrDefaultAsync(x => x.Name.Equals("LastSongList"));
                if (lastPlaylist == null)
                {
                    lastPlaylist = new Setting
                    {
                        Name = "LastSongList"
                    };
                }
                lastPlaylist.IntSetting = playList.Id ?? default(int);
                // await _serviceBackbone.SendChatMessage($"Loaded playlist {0}", playList.Name);
                db.Settings.Update(lastPlaylist);
                await db.SaveChangesAsync();
            }
            await _hubContext.Clients.All.SendAsync("UpdateCurrentPlaylist", BackupPlaylist);
            //await _serviceBackbone.SendChatMessage($"{BackupPlaylist.Name} loaded with {BackupPlaylist.Songs.Count} songs");
        }

        private async Task ImportPlaylist(CommandEventArgs e)
        {
            try
            {
                await _serviceBackbone.SendChatMessage("Importing Playlist");
                var playListName = e.Args[0];
                var playListFile = e.Args[1];
                if (string.IsNullOrEmpty(playListFile) || string.IsNullOrWhiteSpace(playListName))
                {
                    await _serviceBackbone.SendChatMessage("Invalid playlist name or file name");
                    return;
                }

                if (File.Exists($"Playlists/{playListFile}") == false)
                {
                    await _serviceBackbone.SendChatMessage("File doesn't exists");
                    return;
                }

                var songLinks = await File.ReadAllLinesAsync($"Playlists/{playListFile}");
                if (songLinks.Length <= 1)
                {
                    await _serviceBackbone.SendChatMessage("No songs in playlist");
                    return;
                }

                MusicPlaylist? playList;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    playList = await db.Playlists.FirstOrDefaultAsync(x => x.Name.Equals(playListName));
                    if (playList == null)
                    {
                        playList = new MusicPlaylist()
                        {
                            Name = playListName
                        };
                    }
                }

                foreach (var songLink in songLinks)
                {
                    var songId = await GetSongId(songLink);
                    if (string.IsNullOrWhiteSpace(songId))
                    {
                        continue;
                    }
                    var song = await GetSong(songId);
                    if (song == null)
                    {
                        continue;
                    }
                    if (playList.Songs.Where(x => x.SongId.Equals(song.SongId)).FirstOrDefault() != null)
                    {
                        continue;
                    }
                    playList.Songs.Add(song);
                }

                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    db.Playlists.Update(playList);
                    await db.SaveChangesAsync();
                }
                await _serviceBackbone.SendChatMessage($"Imported Playlist {playList.Name} with {playList.Songs.Count()} songs");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import");
                await _serviceBackbone.SendChatMessage("Failed to import playlist");
            }

        }

        private async Task MovePriority(CommandEventArgs e)
        {
            var isCoolDownExpired = await IsCoolDownExpiredWithMessage(e.Name, e.DisplayName, e.Command);
            if (isCoolDownExpired == false) return;
            List<Song> backwardsRequest = new List<Song>();
            lock (RequestsLock)
            {
                backwardsRequest = Requests.ToList();
            }
            backwardsRequest.Reverse();
            foreach (var song in backwardsRequest)
            {
                if (song.RequestedBy.Equals(e.DisplayName))
                {
                    lock (RequestsLock)
                    {
                        Requests.Remove(song);
                        Requests.Insert(0, song);
                    }
                    await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("{0} was moved to next song.", song.Title));
                    NextSong = song;
                    AddCoolDown(e.Name, e.Command, DateTime.Now.AddMinutes(30));
                    await UpdateDbState();
                    return;
                }
            }
            await _serviceBackbone.SendChatMessage(e.DisplayName, "couldn't find a song to prioritize for ya.");
        }

        private async Task SongRequest(CommandEventArgs e)
        {
            var songsInQueue = 0;
            lock (RequestsLock)
            {
                songsInQueue = Requests.Where(x => x.RequestedBy.Equals(e.DisplayName)).Count();
            }
            if (songsInQueue >= 30)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "You already have your quota(30) of songs in the queue.");
                return;
            }
            var searchResult = await GetSongId(e.Arg);
            if (string.IsNullOrWhiteSpace(searchResult))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("Could not get or had an issue finding your song request"));
                return;
            }
            Song? songInQueue = null;
            lock (RequestsLock)
            {
                songInQueue = Requests.Where(x => x.SongId.Equals(searchResult)).FirstOrDefault();
            }
            if (songInQueue != null)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, $"That song is already in the queue.");
                return;
            }

            var song = await GetSong(searchResult, e.DisplayName);
            if (song == null)
            {
                return;
            }
            if (song.Duration > new TimeSpan(0, 10, 0) || song.Duration == new TimeSpan(0, 0, 0))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("Your song is to long or is live. Max is 10 minutes and yours is: {0:c}", song.Duration));
                return;
            }

            song.RequestedBy = e.DisplayName;
            lock (RequestsLock)
            {
                Requests.Add(song);
            }
            await UpdateDbState();
            if (NextSong == null)
            {
                NextSong = song;
            }
            if (e.IsWhisper) return;

            await _serviceBackbone.SendChatMessageWithTitle(e.Name, string.Format("{0} was added to the song queue in position #{1}, you have a total of {2} in queue.", song.Title, Requests.Count, Requests.Where(x => x.RequestedBy.Equals(song.RequestedBy)).Count()));
        }

        private async Task<string> GetSongId(string searchTerm)
        {
            var client = new HttpClient();
            var httpRequest = new HttpRequestMessage()
            {
                RequestUri = new Uri(string.Format("https://beta.decapi.me/youtube/videoid?search={0}", Uri.EscapeDataString(searchTerm))),
                Method = HttpMethod.Get
            };
            var searchResponse = await client.SendAsync(httpRequest);
            var searchResult = await searchResponse.Content.ReadAsStringAsync();
            return searchResult;
        }

        private async Task<Song?> GetSong(string youtubeId, string? displayName = null)
        {
            var ytRequest = _youtubeService.Videos.List("snippet,contentDetails");
            ytRequest.Id = youtubeId;

            var ytResponse = await ytRequest.ExecuteAsync();
            if (ytResponse != null && ytResponse.Items.Count > 0)
            {
                var item = ytResponse.Items.First();
                TimeSpan length = new TimeSpan();
                if (item.ContentDetails.ContentRating.YtRating?.Equals("ytAgeRestricted") == true)
                {
                    await _serviceBackbone.SendChatMessage("That song can not be played due to restrictions.");
                    return null;
                }
                if (item.AgeGating != null && item.AgeGating.Restricted == true)
                {
                    await _serviceBackbone.SendChatMessage("That song can not be played due to restrictions.");
                    return null;
                }
                if (Iso8601DurationHelper.Duration.TryParse(item.ContentDetails.Duration, out var duration))
                {
                    length = new TimeSpan((int)duration.Hours, (int)duration.Minutes, (int)duration.Seconds);
                }

                return new Song
                {
                    Title = item.Snippet.Title,
                    Duration = length,
                    SongId = youtubeId
                };
            }
            if (displayName != null)
            {
                await _serviceBackbone.SendChatMessage(displayName, string.Format("Could not get or had an issue finding your song request"));
            }
            return null;
        }

        private async Task UpdateDbState()
        {
            List<Song> requests = new List<Song>();
            lock (RequestsLock)
            {
                requests = Requests.ToList();
            }
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var id = 1;
                if (requests.Count == 0)
                {
                    db.SongRequestViewItems.RemoveRange(db.SongRequestViewItems);
                    await db.SaveChangesAsync();
                    return;
                }
                foreach (var request in requests)
                {
                    bool newRecord = false;
                    var item = await db.SongRequestViewItems.Where(x => x.Id == id).FirstOrDefaultAsync();
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
                        db.Add(item);
                    }
                    else
                    {
                        db.Update(item);
                    }
                    id++;
                }
                db.SongRequestViewItems.RemoveRange(db.SongRequestViewItems.Where(x => x.Id >= id));
                await db.SaveChangesAsync();
            }
        }
    }
}