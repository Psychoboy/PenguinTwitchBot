using Discord;
using Discord.Net;
using Discord.WebSocket;
using DotNetTwitchBot.Bot.Commands.Custom;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.StreamSchedule;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Bot.Core
{
    public class DiscordService : IDiscordService, IHostedService
    {
        private DiscordSocketClient _client;
        private readonly ILogger<DiscordService> _logger;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly CustomCommand _customCommands;
        private readonly ITwitchService _twitchService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly DiscordSettings _settings;
        private bool isReady = false;

        public DiscordService(
            CustomCommand customCommands,
            ILogger<DiscordService> logger,
            IServiceBackbone serviceBackbone,
            ITwitchService twitchService,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceBackbone = serviceBackbone;
            _serviceBackbone.StreamStarted += StreamStarted;
            _customCommands = customCommands;
            _twitchService = twitchService;
            _scopeFactory = scopeFactory;
            
            var settings = configuration.GetRequiredSection("Discord").Get<DiscordSettings>() ?? throw new Exception("Invalid Configuration. Discord settings missing.");
            _settings = settings;
            
        }

        public ConnectionState ServiceStatus()
        {
            return _client.ConnectionState;
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

        private async Task AnnounceLive()
        {
            try
            {
                _logger.LogInformation("[DISCORD] Waiting 30 seconds to do announcement");
                await Task.Delay(30000); //Delay to generate thumbnail
                _logger.LogInformation("[DISCORD] Doing announcement");
                IGuild guild = _client.GetGuild(_settings.DiscordServerId);

                var channel = (IMessageChannel)await guild.GetChannelAsync(_settings.BroadcastChannel);
                var imageUrl = await _twitchService.GetStreamThumbnail();

                imageUrl = imageUrl.Replace("{width}", "400").Replace("{height}", "225");
                _logger.LogInformation("Thumbnail Url: {imageUrl}", imageUrl);
                var embed = new EmbedBuilder()
                    .WithColor(100, 65, 164)
                    .WithThumbnailUrl("https://static-cdn.jtvnw.net/jtv_user_pictures/7397d16d-a2ff-4835-8f63-249b4738581b-profile_image-300x300.png")
                    .WithTitle("SuperPenguinTV has just went live on Twitch!")
                    .AddField("Now Playing", await _twitchService.GetCurrentGame())
                    .AddField("Stream Title", await _twitchService.GetStreamTitle())
                    .WithUrl("https://twitch.tv/SuperPenguinTV")
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
            IGuild guild = _client.GetGuild(_settings.DiscordServerId);
            return guild.GetEventsAsync();
        }

        public Task<IGuildScheduledEvent> GetEvent(ulong id)
        {
            IGuild guild = _client.GetGuild(_settings.DiscordServerId);
            return guild.GetEventAsync(id);
        }

        public async Task UpdateEvent(IGuildScheduledEvent evt, string title, DateTime startTime, DateTime endTime)
        {
            await evt.ModifyAsync(x =>
            {
                x.StartTime = (DateTimeOffset)DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
                x.EndTime = (DateTimeOffset)DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
                x.Name = title;
            });
        }

        public async Task DeleteEvent(IGuildScheduledEvent evt)
        {
            await evt.DeleteAsync();
        }

        public async Task<ulong> CreateScheduledEvent(ScheduledStream scheduledStream)
        {
            //1033836361653964851
            IGuild guild = _client.GetGuild(_settings.DiscordServerId);
            var evt = await guild.CreateEventAsync(scheduledStream.Title, scheduledStream.Start, GuildScheduledEventType.External, GuildScheduledEventPrivacyLevel.Private, endTime: scheduledStream.End, location: "https://twitch.tv/superpenguintv");
            return evt.Id;
        }

        public async Task DeletePostedScheduled(ulong id)
        {
            IGuild guild = _client.GetGuild(_settings.DiscordServerId);
            var channel = (IMessageChannel)await guild.GetChannelAsync(1033836361653964851);
            await channel.DeleteMessageAsync(id);
        }

        public async Task UpdatePostedSchedule(ulong id, List<ScheduledStream> scheduledStreams)
        {
            IGuild guild = _client.GetGuild(_settings.DiscordServerId);
            var channel = (IMessageChannel)await guild.GetChannelAsync(1033836361653964851);
            await channel.ModifyMessageAsync(id, x =>
            {
                x.Embed = GenerateScheduleEmbed(scheduledStreams);
            });
        }

        private static Embed GenerateScheduleEmbed(List<ScheduledStream> scheduledStreams)
        {
            var embed = new EmbedBuilder().WithColor(100, 65, 164)
               .WithTitle("Stream Schedule");
            foreach (var schedule in scheduledStreams)
            {
                var scheduledTime = new TimestampTag(schedule.Start, TimestampTagStyles.LongDateTime);
                var scheduleTimeRemaining = new TimestampTag(schedule.Start, TimestampTagStyles.Relative);
                embed.AddField(schedule.Title, $"{scheduledTime} {scheduleTimeRemaining}");
            }
            return embed.Build();
        }

        public async Task<ulong> PostSchedule(List<ScheduledStream> scheduledStreams)
        {
            IGuild guild = _client.GetGuild(_settings.DiscordServerId);
            var channel = (IMessageChannel)await guild.GetChannelAsync(1033836361653964851);
            var embed = GenerateScheduleEmbed(scheduledStreams);
            var msg = await channel.SendMessageAsync("", embed: embed);
            return msg.Id;
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

        private async Task MessageReceived(SocketMessage arg)
        {
            if (arg.Type != MessageType.Default && arg.Type != MessageType.Reply) return;
            if (arg.Author is IGuildUser == false) return;
            var user = arg.Author as IGuildUser;
            var message = await arg.Channel.GetMessageAsync(arg.Id);
            if (string.IsNullOrWhiteSpace(message.Content.Trim())) return;
            _logger.LogInformation("[DISCORD] [#{MessageChannelName}] {UserDisplayName}: {MessageContent}", message.Channel.Name, user?.DisplayName, message.Content);
        }

        private async Task SlashCommandHandler(SocketSlashCommand arg)
        {
            var eventArgs = new CommandEventArgs
            {
                Command = arg.CommandName,
                DiscordMention = arg.User.Mention,
                IsDiscord = true
            };

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

            try
            {
                if (_customCommands.CustomCommandExists(arg.CommandName) == false) return;
                var commandResponse = _customCommands.CustomCommandResponse(arg.CommandName);
                var message = await _customCommands.ProcessTags(eventArgs, commandResponse);
                if (message != null && message.Cancel == false && message.Message.Length > 0)
                {
                    await arg.RespondAsync(message.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running custom command via discord");
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

        private async Task OnReady()
        {
            IGuild guild = _client.GetGuild(_settings.DiscordServerId);
            await guild.DownloadUsersAsync(); //Load all users
            var users = await guild.GetUsersAsync();
            foreach (var user in users)
            {
                var activities = user.Activities;
                if (activities.Where(x => x.Type == ActivityType.Streaming && x.Name.Equals("Twitch")).Any())
                {
                    await UserStreaming(user, true);
                }
                else if (user.RoleIds.Where(x => x == 679556411067465735).Any())
                {
                    await UserStreaming(user, false);
                }
            }
            {
                var guildCommand = new SlashCommandBuilder();
                guildCommand.WithName("gib");
                guildCommand.WithDescription("Gib Stuff");
                try
                {
                    await guild.CreateApplicationCommandAsync(guildCommand.Build());
                }
                catch (HttpException exception)
                {
                    _logger.LogError(exception, "Error creating command");
                }
            }
            {
                var guildCommand = new SlashCommandBuilder();
                guildCommand.WithName("dadjoke");
                guildCommand.WithDescription("Get a dad joke");
                try
                {
                    await guild.CreateApplicationCommandAsync(guildCommand.Build());
                }
                catch (HttpException exception)
                {
                    _logger.LogError(exception, "Error creating command");
                }
            }
            {
                var guildCommand = new SlashCommandBuilder();
                guildCommand.WithName("weather");
                guildCommand.WithDescription("Get current weather");
                guildCommand.AddOption("location", ApplicationCommandOptionType.String, "Location you would like to get weather for. Can be City, State, Zip, etc...");
                try
                {
                    await guild.CreateApplicationCommandAsync(guildCommand.Build());
                }
                catch (HttpException exception)
                {
                    _logger.LogError(exception, "Error creating command");
                }
            }
            isReady = true;
            _logger.LogInformation("Discord Bot is ready.");
        }

        private async Task UserStreaming(IGuildUser user, bool isStreaming)
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

        private async Task Connected()
        {
            _logger.LogInformation("Discord Bot Connected.");
            if (isReady)
            {
                IGuild guild = _client.GetGuild(_settings.DiscordServerId);
                await guild.DownloadUsersAsync(); //Load all users
                var users = await guild.GetUsersAsync();
                foreach (var user in users)
                {
                    var activities = user.Activities;
                    if (activities.Where(x => x.Type == ActivityType.Streaming && x.Name.Equals("Twitch")).Any())
                    {
                        await UserStreaming(user, true);
                    }
                    else if (user.RoleIds.Where(x => x == 679556411067465735).Any())
                    {
                        await UserStreaming(user, false);
                        _logger.LogInformation("Removed live streaming role from user on reconnect.");
                    }
                }
            }
        }

        private async Task Initialize(string? discordToken)
        {
            await _client.LoginAsync(TokenType.Bot, discordToken);
            await _client.StartAsync();
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
            _logger.Log(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
            await Task.CompletedTask;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildPresences | GatewayIntents.GuildMembers,
                AlwaysDownloadUsers = true
            };
            _client = new DiscordSocketClient(config);

            _client.Connected += Connected;
            _client.Ready += OnReady;
            _client.SlashCommandExecuted += SlashCommandHandler;
            _client.PresenceUpdated += PresenceUpdated;
            _client.MessageReceived += MessageReceived;
            _client.Disconnected += Disconnected;
            await Initialize(_settings.DiscordToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.Connected -= Connected;
            _client.Ready -= OnReady;
            _client.SlashCommandExecuted -= SlashCommandHandler;
            _client.PresenceUpdated -= PresenceUpdated;
            _client.MessageReceived -= MessageReceived;
            _client.Disconnected -= Disconnected;
            return Task.CompletedTask;
        }
    }
}