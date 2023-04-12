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
            if (Requests.Count > 0)
            {
                var song = Requests.First();
                Requests.RemoveAt(0);
                LastSong = CurrentSong;
                CurrentSong = song;
                NextSong = Requests.FirstOrDefault();
                SkipVotes.Clear();

                return song.SongId;
            }

            if (BackupPlaylist.Songs.Count == 0)
            {
                await LoadBackupList();
            }
            var randomSong = Tools.RandomElement(BackupPlaylist.Songs);
            LastSong = CurrentSong;
            CurrentSong = randomSong;
            NextSong = null;
            SkipVotes.Clear();
            return randomSong.SongId;
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
                        return;
                    }
                }
                {
                    var playList = await db.Playlists.Include(x => x.Songs).OrderBy(x => x.Id).LastOrDefaultAsync();
                    if (playList != null && playList.Songs != null && playList.Songs.Count > 0)
                    {
                        BackupPlaylist = playList;
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
            }
        }

        private async Task WrongSong(CommandEventArgs e)
        {
            var song = Requests.Where(x => x.RequestedBy.Equals(e.DisplayName)).FirstOrDefault();
            if (song != null)
            {
                Requests.Remove(song);
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
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var playList = await db.Playlists.FirstOrDefaultAsync(x => x.Name.Equals(e.Arg));
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
                        Name = "LastSongList",
                        IntSetting = playList.Id ?? default(int)
                    };
                }
            }
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
                await _serviceBackbone.SendChatMessage($"Imported Playlist {playList.Name} with {playList.Songs} songs");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import");
                await _serviceBackbone.SendChatMessage("Failed to import playlist");
            }

        }

        private async Task MovePriority(CommandEventArgs e)
        {
            if (!IsCoolDownExpired(e.Name, e.Command))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "!priority is still on cooldown for you.");
                return;
            }

            foreach (var song in Requests)
            {
                if (song.RequestedBy.Equals(e.DisplayName))
                {
                    Requests.Remove(song);
                    Requests.Insert(0, song);
                    await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("{0} was moved to next song.", song.Title));
                    AddCoolDown(e.Name, e.Command, 60 * 30);
                    return;
                }
            }
            await _serviceBackbone.SendChatMessage(e.DisplayName, "couldn't find a song to prioritize for ya.");
        }

        private async Task SongRequest(CommandEventArgs e)
        {
            if (Requests.Where(x => x.RequestedBy.Equals(e.DisplayName)).Count() >= 30)
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

            if (Requests.Where(x => x.SongId.Equals(searchResult)).FirstOrDefault() != null)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, $"That song is already in the queue.");
                return;
            }

            var song = await GetSong(searchResult);
            if (song == null)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("Could not get or had an issue finding your song request"));
                return;
            }
            if (song.Duration > new TimeSpan(0, 10, 0))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("Your song is to long. Max is 10 minutes and yours is: {0:c}", song.Duration));
                return;
            }

            song.RequestedBy = e.DisplayName;
            Requests.Add(song);
            if (NextSong == null)
            {
                NextSong = song;
            }

            await _serviceBackbone.SendChatMessageWithTitle(e.Name, string.Format("{0} was added to the song queue in position #{1}, you have a total of {2} in count.", song.Title, Requests.Count, Requests.Where(x => x.RequestedBy.Equals(song.RequestedBy)).Count()));
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

        private async Task<Song?> GetSong(string youtubeId)
        {
            var ytRequest = _youtubeService.Videos.List("snippet,contentDetails");
            ytRequest.Id = youtubeId;

            var ytResponse = await ytRequest.ExecuteAsync();
            if (ytResponse != null && ytResponse.Items.Count > 0)
            {
                var item = ytResponse.Items.First();
                TimeSpan length = new TimeSpan();
                if (item.AgeGating != null && item.AgeGating.Restricted == true)
                {
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
            return null;
        }


    }
}