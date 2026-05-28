using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using PenguinTwitchBot.Application.Discord;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models;
using PenguinTwitchBot.Bot.StreamSchedule;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Repository;

namespace PenguinTwitchBot.Bot.Core
{
    public class DiscordService : IDiscordService, IHostedService
    {
        private DiscordSocketClient _client;
        private readonly ILogger<DiscordService> _logger;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly ITwitchService _twitchService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Application.Notifications.IPenguinDispatcher _dispatcher;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConfiguration _configuration;
        private readonly SemaphoreSlim _restartLock = new(1, 1);
        private DiscordSettings _settings;
        private readonly string _broadcaster;
        private bool isReady = false;

        public DiscordService(
            ILogger<DiscordService> logger,
            IServiceBackbone serviceBackbone,
            ITwitchService twitchService,
            IServiceScopeFactory scopeFactory,
            Application.Notifications.IPenguinDispatcher dispatcher,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceBackbone = serviceBackbone;
            _serviceBackbone.StreamStarted += StreamStarted;
            _twitchService = twitchService;
            _scopeFactory = scopeFactory;
            _dispatcher = dispatcher;
            _loggerFactory = loggerFactory;
            _configuration = configuration;

            _settings = LoadSettings();
            _broadcaster = configuration["broadcaster"] ?? "";
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds
                    | GatewayIntents.GuildMembers
                    | GatewayIntents.GuildPresences
                    | GatewayIntents.GuildMessages
                    | GatewayIntents.GuildInvites
                    | GatewayIntents.GuildVoiceStates
                    | GatewayIntents.GuildScheduledEvents
                    | GatewayIntents.MessageContent,
                AlwaysDownloadUsers = false,
                MessageCacheSize = 100
            };
            _client = new DiscordSocketClient(config);
        }

        private DiscordSettings LoadSettings()
        {
            return _configuration.GetRequiredSection("Discord").Get<DiscordSettings>()
                ?? throw new Exception("Invalid Configuration. Discord settings missing.");
        }

        public ConnectionState ServiceStatus()
        {
            return _client.ConnectionState;
        }

        public void SetReady(bool ready)
        {
            isReady = ready;
        }

        private Task Disconnected(Exception exception)
        {
            _logger.LogInformation("Discord Bot Disconnected.");
            return Task.CompletedTask;
        }

        private async Task StreamStarted(object? sender, EventArgs _)
        {
#if DEBUG
            _logger.LogInformation("[DISCORD] Stream Is online - Not Announcing");
            await Task.CompletedTask;
#else
            _logger.LogInformation("[DISCORD] Stream Is online - Announcing soon");
            await AnnounceLive();
#endif

        }

        private SocketGuild GetGuild()
        {
            return _client.GetGuild(_settings.DiscordServerId);
        }

        private async Task AnnounceLive()
        {
            try
            {
                _logger.LogInformation("[DISCORD] Waiting 30 seconds to do announcement");
                await Task.Delay(30000); //Delay to generate thumbnail
                _logger.LogInformation("[DISCORD] Doing announcement");
                IGuild guild = GetGuild();

                var channel = (IMessageChannel)await guild.GetChannelAsync(_settings.BroadcastChannel);
                var imageUrl = await _twitchService.GetStreamThumbnail();

                imageUrl = imageUrl.Replace("{width}", "400").Replace("{height}", "225");
                _logger.LogInformation("Thumbnail Url: {imageUrl}", imageUrl);
                var embed = new EmbedBuilder()
                    .WithColor(100, 65, 164)
                    .WithThumbnailUrl("https://static-cdn.jtvnw.net/jtv_user_pictures/7397d16d-a2ff-4835-8f63-249b4738581b-profile_image-300x300.png")
                    .WithTitle($"{_broadcaster} has just went live on Twitch!")
                    .AddField("Now Playing", await _twitchService.GetCurrentGame())
                    .AddField("Stream Title", await _twitchService.GetStreamTitle())
                    .WithUrl($"https://twitch.tv/{_broadcaster}")
                    .WithImageUrl(imageUrl)
                    .WithCurrentTimestamp()
                    .WithFooter("Twitch").Build();
                var message = "";

                if (_settings.PingRoleWhenLive != 0)
                {
                    var role = guild.GetRole(_settings.PingRoleWhenLive);
                    message += role.Mention;
                }
                var msg = await channel.SendMessageAsync(message, embed: embed);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting live.");
            }
            _logger.LogInformation("[DISCORD] Did announcement");
        }

        public Task<IReadOnlyCollection<IGuildScheduledEvent>> GetEvents()
        {
            if (string.IsNullOrWhiteSpace(_settings.DiscordToken))
            {
                return Task.FromResult<IReadOnlyCollection<IGuildScheduledEvent>>([]);
            }
            IGuild guild = GetGuild();
            return guild.GetEventsAsync();
        }

        public Task<IGuildScheduledEvent?> GetEvent(ulong id)
        {
            if (string.IsNullOrWhiteSpace(_settings.DiscordToken))
            {
                return Task.FromResult<IGuildScheduledEvent?>(null);
            }
            IGuild guild = GetGuild();
            return guild.GetEventAsync(id);
        }

        public ulong GetConnectedAsId()
        {
            return _client.CurrentUser.Id;
        }

        public IReadOnlyList<DiscordGuildInfo> GetCachedGuilds()
        {
            if (string.IsNullOrWhiteSpace(_settings.DiscordToken)) return [];
            return [.. _client.Guilds.Select(g => new DiscordGuildInfo(g.Id, g.Name))];
        }

        public IReadOnlyList<DiscordChannelInfo> GetCachedTextChannels(ulong guildId)
        {
            var guild = _client.GetGuild(guildId);
            if (guild is null) return [];
            return [.. guild.TextChannels.OrderBy(c => c.Position).Select(c => new DiscordChannelInfo(c.Id, c.Name))];
        }

        public IReadOnlyList<DiscordRoleInfo> GetCachedRoles(ulong guildId)
        {
            var guild = _client.GetGuild(guildId);
            if (guild is null) return [];
            return [.. guild.Roles.Where(r => !r.IsEveryone).OrderByDescending(r => r.Position).Select(r => new DiscordRoleInfo(r.Id, r.Name))];
        }

        public async Task UpdateEvent(IGuildScheduledEvent evt, string title, DateTime startTime, DateTime endTime)
        {
            if (string.IsNullOrWhiteSpace(_settings.DiscordToken))
            {
                return;
            }
            try
            {
                await evt.ModifyAsync(x =>
                {
                    x.StartTime = (DateTimeOffset)DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
                    x.EndTime = (DateTimeOffset)DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
                    x.Name = title;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event");
            }
        }

        public async Task DeleteEvent(IGuildScheduledEvent evt)
        {
            if (string.IsNullOrWhiteSpace(_settings.DiscordToken))
            {
                return;
            }
            await evt.DeleteAsync();
        }

        public async Task<ulong> CreateScheduledEvent(ScheduledStream scheduledStream)
        {
            if (string.IsNullOrWhiteSpace(_settings.DiscordToken))
            {
                return 0;
            }
            IGuild guild = GetGuild();
            var evt = await guild.CreateEventAsync(scheduledStream.Title, scheduledStream.Start, GuildScheduledEventType.External, GuildScheduledEventPrivacyLevel.Private, endTime: scheduledStream.End, location: $"https://twitch.tv/{_broadcaster}");
            scheduledStream.DiscordEventId = evt.Id;
            return evt.Id;
        }

        private async Task<string> GetInviteLinkForSchedule()
        {
            IGuild guild = GetGuild();
            var channel = (ITextChannel)await guild.GetChannelAsync(1033836361653964851);
            var invites = await channel.GetInvitesAsync();
            var existingInvite = invites.Where(x => x.ExpiresAt== null).FirstOrDefault();
            if (existingInvite != null)
            {
                return existingInvite.Url;
            }
            var invite = await channel.CreateInviteAsync(maxAge: null);
            return invite.Url;
        }

        public async Task DeletePostedScheduled(ulong id)
        {
            if (string.IsNullOrWhiteSpace(_settings.DiscordToken))
            {
                return;
            }
            try
            {
                IGuild guild = GetGuild();
                var channel = (IMessageChannel)await guild.GetChannelAsync(1033836361653964851);
                await channel.DeleteMessageAsync(id);
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting posted schedule");
            }
        }

        public async Task UpdatePostedSchedule(ulong id, List<ScheduledStream> scheduledStreams)
        {
            if (string.IsNullOrWhiteSpace(_settings.DiscordToken))
            {
                return;
            }
            try
            {
                IGuild guild = GetGuild();
                var channel = (IMessageChannel)await guild.GetChannelAsync(1033836361653964851);
                var embed = await GenerateScheduleEmbed(scheduledStreams);
                await channel.ModifyMessageAsync(id, x =>
                {
                    x.Embed = embed;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating posted schedule");
            }
        }

        private async Task<Embed> GenerateScheduleEmbed(List<ScheduledStream> scheduledStreams)
        {
            var embed = new EmbedBuilder().WithColor(100, 65, 164)
               .WithTitle("Stream Schedule");
            foreach (var schedule in scheduledStreams)
            {
                var scheduledTime = new TimestampTag(schedule.Start, TimestampTagStyles.LongDateTime);
                var scheduleTimeRemaining = new TimestampTag(schedule.Start, TimestampTagStyles.Relative);
                var twitchEventId = await GetDiscordEventIdFromTwitchEventId(schedule.TwitchEventId);
                var inviteUrl = await GetInviteLinkForSchedule();
                if (twitchEventId != 0)
                {
                    embed.AddField(schedule.Title, $"{scheduledTime} {scheduleTimeRemaining} [Join]({inviteUrl}?event={twitchEventId})");
                }
                else
                {
                    embed.AddField(schedule.Title, $"{scheduledTime} {scheduleTimeRemaining}");
                }
            }
            return embed.Build();
        }

        public async Task<ulong> PostSchedule(List<ScheduledStream> scheduledStreams)
        {
            if (string.IsNullOrWhiteSpace(_settings.DiscordToken))
            {
                return 0;
            }
            IGuild guild = GetGuild();
            var channel = (IMessageChannel)await guild.GetChannelAsync(1033836361653964851);
            var embed = await GenerateScheduleEmbed(scheduledStreams);
            var msg = await channel.SendMessageAsync("", embed: embed);
            return msg.Id;
        }

        private async Task<ulong> GetDiscordEventIdFromTwitchEventId(string twitchEventId)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var twitchDiscordEvent = await db.DiscordTwitchEventMap.Find(x => x.TwitchEventId.Equals(twitchEventId)).FirstOrDefaultAsync();
            if (twitchDiscordEvent == null)
            {
                return 0;
            }
            return twitchDiscordEvent.DiscordEventId;
        }

        private async Task PresenceUpdated(SocketUser arg1, SocketPresence before, SocketPresence after)
        {
            if (arg1 is IGuildUser == false) return;
            try
            {
                if (before == null || after == null || before.Activities == null || after.Activities == null) return;
                var user = (IGuildUser)arg1;
                if (before.Activities.Where(x => x.Type == ActivityType.Streaming && x.Name.Equals("Twitch")).Any() && !after.Activities.Where(x => x.Type == ActivityType.Streaming).Any())
                {
                    await UserStreaming(user, false);
                }
                else if (!before.Activities.Where(x => x.Type == ActivityType.Streaming).Any() && after.Activities.Where(x => x.Type == ActivityType.Streaming && x.Name.Equals("Twitch")).Any())
                {
                    await UserStreaming(user, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error with PresenceUpdated");
            }
        }

        private Task MessageReceived(SocketMessage arg)
        {
            if (arg.Type != MessageType.Default && arg.Type != MessageType.Reply) return Task.CompletedTask;
            if (arg.Author is IGuildUser == false) return Task.CompletedTask;
            var user = arg.Author as IGuildUser;
            if (string.IsNullOrWhiteSpace(arg.Content.Trim())) return Task.CompletedTask;
            _logger.LogInformation("[DISCORD] [#{MessageChannelName}] {UserDisplayName}: {MessageContent}", arg.Channel.Name, user?.DisplayName, arg.Content);
            return Task.CompletedTask;
        }

        private async Task SlashCommandHandler(SocketSlashCommand arg)
        {
            if (arg.CommandName.Equals("weather"))
            {
                var options = arg.Data.Options;
                var loc = "";
                if (options.Count != 0)
                {
                    loc = options.First().Value.ToString();
                }
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var weather = scope.ServiceProvider.GetRequiredService<Commands.Misc.Weather>();
                    await arg.RespondAsync(await weather.GetWeather(loc));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting weather");
                }
                return;
            }

            if (arg.CommandName.Equals("dadjoke"))
            {
                try
                {
                    await DoDadJoke(arg);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running dadjoke");
                }

                return;
            }
        }

        private static async Task DoDadJoke(SocketSlashCommand arg)
        {
            var httpClient = new HttpClient();
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri("https://icanhazdadjoke.com/"),
                Method = HttpMethod.Get
            };
            request.Headers.Add("Accept", "text/plain");
            var result = await httpClient.SendAsync(request);
            if (result.IsSuccessStatusCode)
            {
                var joke = await result.Content.ReadAsStringAsync();
                await arg.RespondAsync(joke);
            }
        }

        private Task OnReady()
        {
            var _ = _dispatcher.Publish(new DiscordReadyNotification(_client));
            return Task.CompletedTask;
        }

        public async Task UserStreaming(IGuildUser user, bool isStreaming)
        {
            if (isStreaming)
            {
                _logger.LogInformation("User {DisplayName} started streaming.", user.DisplayName);
                if (_settings.RoleIdToAssignMemberWhenLive != 0)
                {
                    await user.AddRoleAsync(_settings.RoleIdToAssignMemberWhenLive);
                }
            }
            else
            {
                _logger.LogInformation("User {DisplayName} stopped streaming.", user.DisplayName);
                if (_settings.RoleIdToAssignMemberWhenLive != 0)
                {
                    await user.RemoveRoleAsync(_settings.RoleIdToAssignMemberWhenLive);
                }
            }
        }

        private Task Connected()
        {
            _logger.LogInformation("Discord Bot Connected.");
            if (isReady)
            {
               var _ = _dispatcher.Publish(new DiscordConnectedNotification(_client));
            }
            return Task.CompletedTask;
        }

        private async Task Initialize(string? discordToken)
        {
            try
            {
                await _client.LoginAsync(TokenType.Bot, discordToken);
                await _client.StartAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting discord bot");
            }
        }

        public async Task LogAsync(LogMessage message)
        {
            var severity = message.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Trace,
                LogSeverity.Debug => LogLevel.Debug,
                _ => LogLevel.Information
            };
            //_logger.Log(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
            ILogger logger = _loggerFactory.CreateLogger<DiscordSocketClient>();
            logger.Log(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
            await Task.CompletedTask;
        }

        

        private async Task UserUpdated(SocketUser oldUserInfo, SocketUser newUserInfo)
        {
            var olderUserName = oldUserInfo.Username;
            var newUserName = newUserInfo.Username;
            if (!olderUserName.Equals(newUserName)) return;
            var guild = _client.Guilds.FirstOrDefault();
            if (guild == null)
            {
                _logger.LogWarning("Guild was null when got UserUpdated.");
                return;
            }
            if(olderUserName == newUserName) return;
            _logger.LogInformation("{oldName} changed their name to {newName}", olderUserName, newUserName);

            var embed = new EmbedBuilder()
                .WithColor(Color.Orange)
                .WithThumbnailUrl(newUserInfo.GetDisplayAvatarUrl())
                .WithTitle(newUserInfo.GlobalName)
                .WithDescription(newUserInfo.Mention + " edited message")
                .AddField("Old Name", olderUserName, true)
                .AddField("New Name", newUserName, true)
                .WithCurrentTimestamp()
                .WithFooter(newUserInfo.Id.ToString())
                .Build();
            await SendEmbedToAuditChannel(guild, embed);
        }


        private async Task MessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            if(message.HasValue)
            {
                var embed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithThumbnailUrl(message.Value.Author.GetDisplayAvatarUrl())
                .WithTitle(message.Value.Author.GlobalName)
                .WithDescription(message.Value.Author.Mention + " deleted message")
                .AddField("Deleted Message", !string.IsNullOrWhiteSpace(message.Value.Content) ? message.Value.Content : "No Content")
                .WithCurrentTimestamp()
                .WithFooter(message.Id.ToString())
                .Build();
                var guild = _client.Guilds.FirstOrDefault();
                if (guild == null)
                {
                    _logger.LogWarning("Guild was null when got MessageUpdated.");
                    return;
                }

                await SendEmbedToAuditChannel(guild, embed);
            }
        }

        private static async Task SendEmbedToAuditChannel(SocketGuild guild, Embed embed)
        {
            var auditChannel = (IMessageChannel)guild.GetChannel(679541861861425153);
            if (auditChannel != null)
            {
                await auditChannel.SendMessageAsync(embed: embed, allowedMentions: AllowedMentions.None);
            }
        }

        public static async Task CacheLastMessages(IGuild guild)
        {
            var channels = await guild.GetChannelsAsync();
            foreach (var channel in channels)
            {
                if(channel.GetChannelType() == ChannelType.Text)
                {
                    var textChannel = (IMessageChannel)channel;
                    var messages = textChannel.GetMessagesAsync();
                    await messages.FlattenAsync();
                }
            }
        }

        private async Task MessageUpdated(Cacheable<IMessage, ulong> oldMessageCache, SocketMessage newSocketMessage, ISocketMessageChannel channel)
        {
            var oldMessage = "";
            if(oldMessageCache.HasValue && oldMessageCache.Value != null && !string.IsNullOrWhiteSpace(oldMessageCache.Value.Content.Trim()))
            {
                oldMessage = oldMessageCache.Value.Content.Trim();
            }

            if (string.IsNullOrWhiteSpace(newSocketMessage.Content.Trim())) return;

            if (string.IsNullOrWhiteSpace(oldMessage)) 
                return;

            if(!oldMessage.Equals(newSocketMessage.Content.Trim())) 
                _logger.LogInformation("User {username} updated old Message: {oldMessage} new message: {newMessage}", newSocketMessage.Author.Username, oldMessage, newSocketMessage.Content);

            var guild = _client.Guilds.FirstOrDefault();
            if (guild == null)
            {
                _logger.LogWarning("Guild was null when got MessageUpdated.");
                return;
            }
            var embedBuilder = new EmbedBuilder()
                .WithColor(Color.Orange)
                .WithThumbnailUrl(newSocketMessage.Author.GetDisplayAvatarUrl())
                .WithTitle(newSocketMessage.Author.GlobalName)
                .WithDescription(newSocketMessage.Author.Mention + " edited message")
                .WithCurrentTimestamp()
                .WithFooter(newSocketMessage.Author.Id.ToString());

            if(!string.IsNullOrWhiteSpace(oldMessage))
            {
                embedBuilder.AddField("Old Message", oldMessage);
            }
            var embed = embedBuilder.AddField("New Message", newSocketMessage.Content).Build();

            await SendEmbedToAuditChannel(guild, embed);
        }

        private async Task UserLeft(SocketGuild guild, SocketUser user)
        {
            _logger.LogInformation("{username} {id} left the discord server", user.Username, user.Id);
            var embed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithThumbnailUrl(user.GetDisplayAvatarUrl())
                .WithTitle(user.GlobalName)
                .WithDescription(user.Mention+ " left the server")
                .WithCurrentTimestamp()
                .WithFooter(user.Id.ToString())
                .Build();
            await SendEmbedToAuditChannel(guild, embed);
        }

        private async Task UserJoined(SocketGuildUser guildUser)
        {
            _logger.LogInformation("{username} {id} joined the discord server", guildUser.Username, guildUser.Id);
            var embed = new EmbedBuilder()
               .WithColor(Color.Green)
               .WithThumbnailUrl(guildUser.GetDisplayAvatarUrl())
               .WithTitle(guildUser.GlobalName)
               .WithDescription(guildUser.Mention + " joined the server")
               .AddField("Account Creation", guildUser.CreatedAt)
               .WithCurrentTimestamp()
               .WithFooter(guildUser.Id.ToString())
               .Build();
            var guild = _client.Guilds.FirstOrDefault();
            if (guild == null)
            {
                _logger.LogWarning("Guild was null when got UserJoined.");
                return;
            }
            await SendEmbedToAuditChannel(guild, embed);
        }


        private  Task GuildScheduledEventUpdated(Cacheable<SocketGuildEvent, ulong> cacheable, SocketGuildEvent @event)
        {
            try
            {
                _logger.LogInformation("Event Updated: {EventName}", @event.Name);
            }
            catch (Exception)
            { }
            return Task.CompletedTask;
        }

        private Task GuildScheduledEventUserRemove(Cacheable<SocketUser, RestUser, IUser, ulong> cacheable, SocketGuildEvent @event)
        {
            try
            {
                _logger.LogInformation("Event User Removed: {EventName}", @event.Name);
            }
            catch (Exception)
            {
            }
            return Task.CompletedTask;
        }

        private Task GuildScheduledEventUserAdd(Cacheable<SocketUser, Discord.Rest.RestUser, IUser, ulong> arg1, SocketGuildEvent arg2)
        {
            try
            {
                _logger.LogInformation("Event User Added: {EventName}", arg2.Name);
            }
            catch (Exception)
            {
            }
            return Task.CompletedTask;
        }


        private Task InviteCreated(SocketInvite invite)
        {
            _logger.LogInformation("Invite Created: {InviteCode}", invite.Code);
            return Task.CompletedTask;
        }

        private Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState state1, SocketVoiceState state2)
        {
            if (state2.VoiceChannel != null)
            {
                _logger.LogInformation("User {username} joined voice channel {channelName}", user.Username, state2.VoiceChannel.Name);
            }
            else
            {
                _logger.LogInformation("User {username} left voice channel", user.Username);
            }
            return Task.CompletedTask;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _restartLock.WaitAsync(cancellationToken);
            try
            {
                await StartCoreAsync(cancellationToken);
            }
            finally
            {
                _restartLock.Release();
            }
        }

        private async Task StartCoreAsync(CancellationToken cancellationToken)
        {
            _settings = LoadSettings();

            if (string.IsNullOrWhiteSpace(_settings.DiscordToken))
            {
                _logger.LogInformation("Discord token is not configured — Discord service will not start.");
                return;
            }

            _logger.LogInformation("Starting Discord Service.");
            
            _client.Connected += Connected;
            _client.Ready += OnReady;
            _client.SlashCommandExecuted += SlashCommandHandler;
            _client.PresenceUpdated += PresenceUpdated;
            _client.MessageReceived += MessageReceived;
            _client.Disconnected += Disconnected;
            _client.Log += LogAsync;
            _client.UserJoined += UserJoined;
            _client.UserLeft += UserLeft;
            _client.UserUpdated += UserUpdated;
            _client.MessageUpdated += MessageUpdated;
            _client.MessageDeleted += MessageDeleted;
            _client.GuildScheduledEventUserAdd += GuildScheduledEventUserAdd;
            _client.GuildScheduledEventUserRemove += GuildScheduledEventUserRemove;
            _client.GuildScheduledEventUpdated += GuildScheduledEventUpdated;
            _client.InviteCreated += InviteCreated;
            _client.UserVoiceStateUpdated += UserVoiceStateUpdated;
            await Initialize(_settings.DiscordToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _restartLock.WaitAsync(cancellationToken);
            try
            {
                await StopCoreAsync(cancellationToken);
            }
            finally
            {
                _restartLock.Release();
            }
        }

        private async Task StopCoreAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Discord Service.");
            _client.Connected -= Connected;
            _client.Ready -= OnReady;
            _client.SlashCommandExecuted -= SlashCommandHandler;
            _client.PresenceUpdated -= PresenceUpdated;
            _client.MessageReceived -= MessageReceived;
            _client.Disconnected -= Disconnected;
            _client.Log -= LogAsync;
            _client.UserJoined -= UserJoined;
            _client.UserLeft -= UserLeft;
            _client.UserUpdated -= UserUpdated;
            _client.MessageUpdated -= MessageUpdated;
            _client.MessageDeleted -= MessageDeleted;
            _client.GuildScheduledEventUserAdd -= GuildScheduledEventUserAdd;
            _client.GuildScheduledEventUserRemove -= GuildScheduledEventUserRemove;
            _client.GuildScheduledEventUpdated -= GuildScheduledEventUpdated;
            _client.InviteCreated -= InviteCreated;
            _client.UserVoiceStateUpdated -= UserVoiceStateUpdated;

            if (_client.ConnectionState != ConnectionState.Disconnected)
            {
                await _client.StopAsync();
            }

            if (_client.LoginState != LoginState.LoggedOut)
            {
                await _client.LogoutAsync();
            }
        }

        public async Task RestartAsync(CancellationToken cancellationToken = default)
        {
            await _restartLock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Restarting Discord Service.");
                await StopCoreAsync(cancellationToken);
                _settings = LoadSettings();

                if (string.IsNullOrWhiteSpace(_settings.DiscordToken))
                {
                    _logger.LogInformation("Discord token is not configured after restart request. Leaving Discord service stopped.");
                    return;
                }

                await StartCoreAsync(cancellationToken);
            }
            finally
            {
                _restartLock.Release();
            }
        }
    }
}