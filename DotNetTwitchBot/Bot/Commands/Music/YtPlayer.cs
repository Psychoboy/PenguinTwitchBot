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

namespace DotNetTwitchBot.Bot.Commands.Music
{
    public class YtPlayer : BaseCommand
    {
        private IHubContext<YtHub> _hubContext;
        private IServiceScopeFactory _scopeFactory;
        private YouTubeService _youtubeService;
        private List<Song> Requests = new List<Song>();
        private MusicPlaylist BackupPlaylist = new MusicPlaylist();
        private PlayerState State = PlayerState.UnStarted;
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
            IHubContext<YtHub> hubContext,
            IServiceScopeFactory scopeFactory,
            ServiceBackbone serviceBackbone
        ) : base(serviceBackbone)
        {
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
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
                return song.SongId;
            }

            if (BackupPlaylist.Songs.Count == 0)
            {
                await LoadBackupList();
            }
            var randomSong = BackupPlaylist.Songs[Tools.CurrentThreadRandom.Next(BackupPlaylist.Songs.Count)].SongId;
            return randomSong;
        }

        private async Task LoadBackupList()
        {

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var lastPlaylist = await db.Settings.FirstOrDefaultAsync(x => x.Name.Equals("LastSongList"));
                if (lastPlaylist != null)
                {
                    var playList = await db.Playlists.FirstOrDefaultAsync(x => x.Id == lastPlaylist.IntSetting);
                    if (playList != null && playList.Songs != null && playList.Songs.Count > 0)
                    {
                        BackupPlaylist = playList;
                        return;
                    }
                }
                {
                    var playList = await db.Playlists.LastOrDefaultAsync();
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
                case "testnextsong":
                    await PlayNextSong();
                    break;
                case "testpause":
                    await Pause();
                    break;
                case "testsr":
                    await SongRequest(e);
                    break;
                case "testpriority":
                    await MovePriority(e);
                    break;
                case "testimportpl":
                    await ImportPlaylist(e);
                    break;
                case "testloadpl":
                    await LoadPlaylist(e);
                    break;
            }
        }

        private Task LoadPlaylist(CommandEventArgs e)
        {
            throw new NotImplementedException();
        }

        private Task ImportPlaylist(CommandEventArgs e)
        {
            throw new NotImplementedException();
        }

        private Task ImportPl(CommandEventArgs e)
        {
            throw new NotImplementedException();
        }

        private async Task MovePriority(CommandEventArgs e)
        {
            if (IsCoolDownExpired(e.Name, e.Command))
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
                    await _serviceBackbone.SendChatMessage(e.DisplayName, "{0} was moved to next song.");
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
            var client = new HttpClient();
            var httpRequest = new HttpRequestMessage()
            {
                RequestUri = new Uri(string.Format("https://beta.decapi.me/youtube/videoid?search={0}", e.Arg)),
                Method = HttpMethod.Get
            };
            var searchResponse = await client.SendAsync(httpRequest);
            var searchResult = await searchResponse.Content.ReadAsStringAsync();
            var song = await GetSong(searchResult);
            if (song == null)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("Could not get or issue finding your song request"));
                return;
            }
            if (song.Duration > new TimeSpan(0, 10, 0))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("Your song is to long. Max is 10 minutes and yours is: {0:c}", song.Duration));
                return;
            }

            song.RequestedBy = e.DisplayName;
            Requests.Add(song);

            await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("{0} was added to the song queue in position #{1}", song.Title, Requests.Count));
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